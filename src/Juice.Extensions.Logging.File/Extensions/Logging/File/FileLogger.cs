using System.Collections.Concurrent;
using System.Text;
using Microsoft.Extensions.Logging;
using FileAPI = System.IO.File;
namespace Juice.Extensions.Logging.File
{
    internal class FileLogger : IDisposable
    {
        private ConcurrentQueue<LogEntry> _logQueue = new();

        private string _directory;
        private string _filePath;

        private int _retainPolicyFileCount = 50;
        private int _maxFileSize = 5 * 1024 * 1024;
        private int _counter = 0;
        private TimeSpan _bufferTime = TimeSpan.FromSeconds(5);
        private bool _includeScopes = false;
        private bool _includeCategories = false;
        private bool _fork = false;
        private bool _named = false;
        private readonly StringBuilder _sb = new();

        public FileLogger(string? directory, FileLoggerOptions options, bool named)
        {
            if (options.RetainPolicyFileCount > 0)
            {
                _retainPolicyFileCount = options.RetainPolicyFileCount;
            }
            if (options.MaxFileSize > 0)
            {
                _maxFileSize = options.MaxFileSize;
            }
            _directory = directory ?? "";
            if (options.BufferTime.TotalMilliseconds > 0)
            {
                _bufferTime = options.BufferTime;
            }
            _includeScopes = options.IncludeScopes;
            _includeCategories = options.IncludeCategories;
            _fork = options.ForkJobLog;
            _named = named;
        }
        #region Logging
        /// <summary>
        /// Applies the log file retains policy according to options
        /// </summary>
        private void ApplyRetainPolicy()
        {
            try
            {
                List<FileInfo> files = new DirectoryInfo(_directory)
                .GetFiles("*.log", SearchOption.TopDirectoryOnly)
                .OrderBy(fi => fi.CreationTime)
                .ToList();

                while (files.Count >= _retainPolicyFileCount)
                {
                    var file = files.First();
                    file.Delete();
                    files.Remove(file);
                }
            }
            catch
            {
            }
        }

        /// <summary>
        /// Creates a new disk file and writes the service info
        /// </summary>
        private void BeginFile(string? fileName)
        {
            _counter = 0;
            var dir = Path.Combine(Directory.GetCurrentDirectory(), _directory);
            Directory.CreateDirectory(dir);

            var newFile = Path.Combine(Directory.GetCurrentDirectory(), _directory, fileName ?? DateTimeOffset.Now.ToString("yyyy_MM_dd-HHmm")) + ".log";

            _filePath = newFile;

            ApplyRetainPolicy();
        }

        private string? _originFilePath;
        private bool _forked;
        private void ForkNewFile(string fileName)
        {
            // if provider request to fork to specified fileName, store origial file name to use after
            if (_forked)
            {
                if (fileName.Equals(Path.GetFileNameWithoutExtension(_filePath)))
                {
                    return;
                }
                RestoreOriginFile(default);
            }
            WriteLineAsync("Begin " + fileName + "\n").Wait();

            _forked = true;
            _originFilePath = _filePath;
            BeginFile(fileName);
        }

        private void RestoreOriginFile(string? state)
        {
            if (_forked && !string.IsNullOrEmpty(_originFilePath))
            {
                _forked = false;
                var origin = _filePath;

                if (!string.IsNullOrEmpty(state))
                {
                    origin = RenameFile(_filePath, state);
                }
                _filePath = _originFilePath;
                _originFilePath = default;
                WriteLineAsync("Restore from " + Path.GetFileName(origin) + "\n").Wait();
            }
        }

        private string RenameFile(string filePath, string state)
        {
            if (FileAPI.Exists(filePath))
            {
                var newFile = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + "_" + state + Path.GetExtension(filePath));
                var ver = 0;
                while (FileAPI.Exists(newFile))
                {
                    newFile = Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + "_" + state + " (" + (++ver) + ")" + Path.GetExtension(filePath));
                }

                FileAPI.Move(filePath, newFile);
                return newFile;
            }
            return filePath;
        }

        /// <summary>
        /// Writes a line of text to the current FileAPI.
        /// If the file reaches the size limit, creates a new file and uses that new FileAPI.
        /// </summary>
        private async Task WriteLineAsync(string message)
        {
            // check the file size after any 100 writes
            _counter++;
            if (_counter % 100 == 0)
            {
                if (new FileInfo(_filePath).Length > _maxFileSize)
                {
                    BeginFile(default);
                }
            }
            if (!string.IsNullOrEmpty(message))
            {
                await FileAPI.AppendAllTextAsync(_filePath, message);
            }
        }

        private async Task FlushAsync()
        {
            await WriteLineAsync(_sb.ToString());
            _sb.Clear();
        }

        private List<string> _scopes = new();

        private void WriteInteral(LogEntry log)
        {
            var time = log.Timestamp.ToLocalTime().ToString("HH:mm:ss.ff");
            var includeCategory = _includeCategories || (!_named && !_forked);

            // is inside a scope? or not include category?
            if ((_scopes.Any() && _includeScopes) || !includeCategory)
            {
                _sb.AppendFormat("{0} {1}: {2}", time, GetLevelShortName(log.LogLevel), log.Message);
                _sb.AppendLine();
            }
            else
            {
                _sb.AppendFormat("{0} {1}: {2}", time, GetLevelShortName(log.LogLevel), log.Category);
                _sb.AppendLine();
                _sb.AppendLine(log.Message);
            }
            if (log.Exception != null)
            {
                _sb.AppendLine(log.Exception.StackTrace);
            }
        }

        private static string GetLevelShortName(LogLevel level)
        {
            return level switch
            {
                LogLevel.Trace => "TRC",
                LogLevel.Debug => "DBG",
                LogLevel.Information => "INF",
                LogLevel.Warning => "WRN",
                LogLevel.Error => "ERR",
                LogLevel.Critical => "CRI",
                _ => "NON",
            };
        }

        public void BeginScope<TState>(TState state)
        {
            BeginScopeInternalAsync(state).Wait();
        }
        public void EndScope<TState>(TState state)
        {
            EndScopeInternalAsync(state).Wait();
        }
        private async Task BeginScopeInternalAsync<TState>(TState state)
        {
            if (state is string scope)
            {
                var i = _scopes.IndexOf(scope);
                if (i == -1)
                {
                    var newScopes = new List<string>(_scopes)
                    {
                        scope
                    };
                    BeginScopes(_sb, _scopes, newScopes);
                    _scopes = newScopes;
                }
            }
            else if (state is string[] scopes)
            {
                var newScopes = new List<string>(_scopes);
                newScopes.AddRange(scopes);
                BeginScopes(_sb, _scopes, newScopes);
                _scopes = newScopes;
            }
            else if (state is IEnumerable<KeyValuePair<string, object>> kvps)
            {
                if (_fork && kvps.Any(kvp => kvp.Key == "TraceId"))
                {
                    var traceId = kvps.Single(kvp => kvp.Key == "TraceId").Value?.ToString();
                    var operation = kvps.Single(kvp => kvp.Key == "Operation").Value?.ToString();
                    if (!string.IsNullOrEmpty(traceId))
                    {
                        await FlushAsync();
                        ForkNewFile(FileLoggerProvider.GetForkedFileName(traceId, operation));
                    }
                }
            }
        }
        private async Task EndScopeInternalAsync<TState>(TState state)
        {
            if (state is string scope)
            {
                var i = _scopes.IndexOf(scope);
                if (i >= 0)
                {
                    var newScopes = _scopes.Take(i).ToList();
                    EndScopes(_sb, _scopes, newScopes);
                    _scopes = newScopes;
                    await FlushAsync();
                }
            }
            else if (state is string[] scopes)
            {
                var newScopes = new List<string>(_scopes);
                foreach (var s in scopes)
                {
                    var i = newScopes.LastIndexOf(s);
                    if (i >= 0)
                    {
                        newScopes = newScopes.Take(i).ToList();
                    }
                }
                EndScopes(_sb, _scopes, newScopes);
                _scopes = newScopes;
                await FlushAsync();
            }
            else if (_forked && state is IEnumerable<KeyValuePair<string, object>> kvps
                && kvps.Any(kvp => kvp.Key == "OperationState"
                    || kvp.Key == "TraceId"))
            {
                await FlushAsync();
                RestoreOriginFile(kvps.FirstOrDefault(kvp => kvp.Key == "OperationState").Value?.ToString());
            }
        }

        private void EndScopes(StringBuilder sb, List<string> scopes, List<string> newScopes)
        {
            for (var i = 0; i < scopes.Count; i++)
            {
                if (i >= newScopes.Count)
                {
                    sb.AppendLine();
                    for (var j = scopes.Count - 1; j >= i; j--)
                    {
                        sb.AppendFormat("{0}   End: {1}", new string('-', (j + 1) * 4), scopes[j]);
                        sb.AppendLine();
                    }
                    break;
                }
                if (scopes[i] != newScopes[i])
                {
                    sb.AppendLine();
                    for (var j = scopes.Count - 1; j >= i; j--)
                    {
                        sb.AppendFormat("{0}   End: {1}", new string('-', (j + 1) * 4), scopes[j]);
                        sb.AppendLine();
                    }
                    sb.AppendLine();
                    return;
                }
            }
        }

        private void BeginScopes(StringBuilder sb, List<string> scopes, List<string> newScopes)
        {
            if (_includeScopes)
            {
                for (var i = 0; i < scopes.Count; i++)
                {
                    if (scopes[i] != newScopes[i])
                    {
                        for (var j = i; j < newScopes.Count; j++)
                        {
                            sb.AppendFormat("{0} Begin: {1}", new string('-', (j + 1) * 4), newScopes[j]);
                            sb.AppendLine();
                        }
                        sb.AppendLine();
                        return;
                    }
                }
                if (newScopes.Count > scopes.Count)
                {
                    for (var j = scopes.Count; j < newScopes.Count; j++)
                    {
                        sb.AppendFormat("{0} Begin: {1}", new string('-', (j + 1) * 4), newScopes[j]);
                        sb.AppendLine();
                    }
                    sb.AppendLine();
                }
            }
        }

        /// <summary>
        /// Enqueue log entry
        /// </summary>
        /// <param name="logEntry"></param>
        public void Write(LogEntry logEntry)
        {
            WriteInteral(logEntry);
        }
        #endregion

        #region Service

        protected CancellationTokenSource? _shutdown;
        protected Task? _backgroundTask;

        /// <summary>
        /// Start background task to processing logs queue
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public Task StartAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            BeginFile(default);

            // Create a linked token so we can trigger cancellation outside of this token's cancellation
            _shutdown = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            _backgroundTask = Task.Run(ExecuteAsync);

            return Task.CompletedTask;
        }

        /// <summary>
        /// Stop background task
        /// </summary>
        /// <param name="cancellationToken"></param>
        /// <returns></returns>
        public virtual async Task StopAsync(CancellationToken cancellationToken)
        {
            // Signal cancellation to the executing method
            _shutdown?.Cancel();

            var tasks = new List<Task>();
            // Stop called without start
            if (_backgroundTask != null && !_backgroundTask.IsCompleted)
            {
                tasks.Add(_backgroundTask);
            }
            if (tasks.Any())
            {
                await Task.WhenAny(Task.WhenAll(tasks), Task.Delay(5000, cancellationToken));
            }

            // Throw if cancellation triggered
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Processing logs queue
        /// </summary>
        /// <returns></returns>
        protected async Task ExecuteAsync()
        {
            while (!(_shutdown?.IsCancellationRequested) ?? false)
            {
                try
                {
                    await FlushAsync();
                    await Task.Delay(_bufferTime, _shutdown!.Token);
                }
                catch (TaskCanceledException) { }
            }

            try
            {
                await FlushAsync();
            }
            catch (Exception) { }
        }

        #endregion


        #region IDisposable Support


        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    //  dispose managed state (managed objects).
                    try
                    {
                        _logQueue.Clear();
                    }
                    catch { }
                }
                disposedValue = true;
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        #endregion
    }

}
