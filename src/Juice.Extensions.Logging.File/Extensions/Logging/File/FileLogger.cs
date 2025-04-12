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
        private string? _filePath;

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
            _directory = Path.Combine(Directory.GetCurrentDirectory(), directory ?? "");
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
                .GetFiles("*.log", SearchOption.AllDirectories)
                .OrderBy(fi => fi.CreationTime)
                .ToList();

                while (files.Count >= _retainPolicyFileCount)
                {
                    var file = files.First();
                    var directory = file.Directory;
                    file.Delete();
                    if (directory != null && !directory.EnumerateFiles().Any() && !directory.EnumerateDirectories().Any())
                    {
                        directory.Delete();
                    }
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
            var month = DateTimeOffset.Now.ToString("yyyy_MM");
            var newFile = string.IsNullOrEmpty(fileName)
                ? GetUniqueFilePath(Path.Combine(_directory, month, DateTimeOffset.Now.ToString("yyyy_MM_dd-HHmmss")) + ".log")
                : Path.Combine(_directory, month, DateTimeOffset.Now.ToString("dd"), fileName) + ".log";
            var dir = Path.GetDirectoryName(newFile);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir!);
            }
            if(_filePath != null && FileAPI.Exists(_filePath) && _filePath != newFile)
            {
                FileAPI.AppendAllText(_filePath, "Begin " + Path.GetRelativePath(Path.GetDirectoryName(_filePath) ?? Directory.GetCurrentDirectory(), newFile) + "\n");
            }
            _filePath = newFile;

            ApplyRetainPolicy();
        }

        private static string GetUniqueFilePath(string filePath)
        {
            string directory = Path.GetDirectoryName(filePath)!;
            string fileName = Path.GetFileNameWithoutExtension(filePath);
            string extension = Path.GetExtension(filePath);

            string newPath = filePath;
            int count = 1;

            while (FileAPI.Exists(newPath))
            {
                newPath = Path.Combine(directory, $"{fileName} ({count}){extension}");
                count++;
            }

            return newPath;
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

            _forked = true;
            _originFilePath = _filePath;
            BeginFile(fileName);
        }

        private void RestoreOriginFile(string? state)
        {
            if (_forked && !string.IsNullOrEmpty(_originFilePath) && !string.IsNullOrEmpty(_filePath))
            {
                _forked = false;
                var origin = _filePath;

                if (!string.IsNullOrEmpty(state))
                {
                    origin = RenameFile(_filePath, state);
                }
                var relativePath = Path.GetRelativePath(Path.GetDirectoryName(_originFilePath) ?? Directory.GetCurrentDirectory(), origin);
                _filePath = _originFilePath;
                _originFilePath = default;
                FileAPI.AppendAllText(_filePath, "Restored from " + relativePath + "\n");
            }
        }

        private string RenameFile(string filePath, string state)
        {
            if (FileAPI.Exists(filePath))
            {
                var newFile = GetUniqueFilePath(Path.Combine(Path.GetDirectoryName(filePath)!, Path.GetFileNameWithoutExtension(filePath) + "_" + state + Path.GetExtension(filePath)));
                
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
            try
            {
                if (_filePath == null || string.IsNullOrEmpty(message))
                {
                    return;
                }

                if (FileAPI.Exists(_filePath) && (new FileInfo(_filePath).Length > _maxFileSize || FileAPI.GetCreationTime(_filePath).Date < DateTime.Now.Date))
                {
                    BeginFile(default);
                }

                await FileAPI.AppendAllTextAsync(_filePath, message);
            }
            catch (Exception) { }
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
            if (_sb.Length > 100000)
            {
                _flushWaiter.Cancel();
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
            try
            {
                BeginScopeInternalAsync(state).Wait();
            }
            catch (Exception) { }
        }
        public void EndScope<TState>(TState state)
        {
            try
            {
                EndScopeInternalAsync(state).Wait();
            }
            catch (Exception) { }
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
                var objScopes = kvps.ToList();
                if (_fork && kvps.Any(kvp => kvp.Key == "TraceId"))
                {
                    var traceId = kvps.Single(kvp => kvp.Key == "TraceId").Value?.ToString();
                    var operation = kvps.SingleOrDefault(kvp => kvp.Key == "Operation").Value?.ToString();
                    if (!string.IsNullOrEmpty(traceId))
                    {
                        await FlushAsync();
                        ForkNewFile(FileLoggerProvider.GetForkedFileName(traceId, operation));
                    }
                    objScopes.RemoveAll(s => s.Key == "TraceId" || s.Key == "Operation");
                }
                else
                {
                    var newScopes = new List<string>(_scopes);
                    newScopes.AddRange(objScopes.Select(s => $"{s.Key}: {s.Value}"));
                    BeginScopes(_sb, _scopes, newScopes);
                    _scopes = newScopes;

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
            else if (state is IEnumerable<KeyValuePair<string, object>> kvps)
            {
                var objScopes = kvps.ToList();
                if (_forked)
                {
                    objScopes.RemoveAll(s => s.Key == "OperationState" || s.Key == "TraceId" || s.Key == "Operation");
                }
                if (objScopes.Any())
                {
                    var newScopes = new List<string>(_scopes);
                    foreach (var s in objScopes.Select(s => $"{s.Key}: {s.Value}"))
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
                if (_forked & kvps.Any(kvp => kvp.Key == "OperationState"
                    || kvp.Key == "TraceId"))
                {
                    await FlushAsync();
                    RestoreOriginFile(kvps.FirstOrDefault(kvp => kvp.Key == "OperationState").Value?.ToString());

                }
            }
        }

        private void EndScopes(StringBuilder sb, List<string> scopes, List<string> newScopes)
        {
            if (_includeScopes)
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
                }
            }
        }

        private void BeginScopes(StringBuilder sb, List<string> scopes, List<string> newScopes)
        {
            if (_includeScopes)
            {
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
        protected CancellationTokenSource _flushWaiter;
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
            _flushWaiter = CancellationTokenSource.CreateLinkedTokenSource(_shutdown.Token);
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
                    if (_flushWaiter == null || _flushWaiter.IsCancellationRequested)
                    {
                        _flushWaiter = CancellationTokenSource.CreateLinkedTokenSource(_shutdown!.Token);
                    }

                    await Task.Delay(_bufferTime, _flushWaiter.Token);
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
