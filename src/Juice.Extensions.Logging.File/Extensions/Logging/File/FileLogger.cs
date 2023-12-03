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

        public FileLogger(string? directory, FileLoggerOptions options)
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
            if (_forked) { return; }
            _forked = true;
            _originFilePath = _filePath;
            BeginFile(fileName);
        }

        private void RestoreOriginFile(string? state)
        {
            _forked = false;
            if (!string.IsNullOrEmpty(_originFilePath))
            {
                if (!string.IsNullOrEmpty(state))
                {
                    var newFile = Path.Combine(Path.GetDirectoryName(_filePath)!, Path.GetFileNameWithoutExtension(_filePath) + "_" + state + Path.GetExtension(_filePath));
                    var ver = 0;
                    while (FileAPI.Exists(newFile))
                    {
                        newFile = Path.Combine(Path.GetDirectoryName(_filePath)!, Path.GetFileNameWithoutExtension(_filePath) + "_" + state + " (" + (++ver) + ")" + Path.GetExtension(_filePath));
                    }

                    FileAPI.Move(_filePath, newFile);
                }
                _filePath = _originFilePath;
                _originFilePath = default;
            }
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

        private List<string> _scopes = new();
        /// <summary>
        /// Dequeue and write log entry to file
        /// </summary>
        private async Task WriteFromQueueAsync(bool stop = false)
        {
            var sb = new StringBuilder();
            string? state = default!;
            while (_logQueue.TryDequeue(out var log))
            {
                if (log.FileName == null)
                {
                    if (_forked)
                    {
                        await WriteLineAsync(sb.ToString());
                        sb.Clear();
                        RestoreOriginFile(state);
                    }
                }
                else
                {
                    if (!_forked)
                    {
                        await WriteLineAsync(sb.ToString());
                        sb.Clear();
                        ForkNewFile(log.FileName);
                    }
                    if (!string.IsNullOrEmpty(log.State))
                    {
                        state = log.State;
                    }
                }

                var scopes = log.GetScopes();
                CompareAndBuildScope(sb, _scopes, scopes, log.Category);
                _scopes = scopes;

                var time = log.Timestamp.ToLocalTime().ToString("HH:mm:ss.ff");
                if (scopes.Any() && _includeScopes)
                {
                    sb.AppendFormat("{0} {1}: {2}", time, GetLevelShortName(log.LogLevel), log.Message);
                    sb.AppendLine();
                }
                else
                {
                    sb.AppendFormat("{0} {1}: {2}", time, GetLevelShortName(log.LogLevel), log.Category);
                    sb.AppendLine();
                    sb.AppendLine(log.Message);
                }
                if (log.Exception != null)
                {
                    sb.AppendLine(log.Exception.StackTrace);
                }
            }
            if (stop)
            {
                CompareAndBuildScope(sb, _scopes, new List<string>(), default);
            }
            await WriteLineAsync(sb.ToString());
            sb.Clear();
            if (_forked)
            {
                RestoreOriginFile(state);
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

        private void CompareAndBuildScope(StringBuilder sb, List<string> scopes, List<string> newScopes, string? caterogy)
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
                    if (scopes[i] != newScopes[i])
                    {
                        sb.AppendLine();
                        for (var j = scopes.Count - 1; j >= i; j--)
                        {
                            sb.AppendFormat("{0}   End: {1}", new string('-', (j + 1) * 4), scopes[j]);
                            sb.AppendLine();
                        }
                        for (var j = i; j < newScopes.Count; j++)
                        {
                            sb.AppendFormat("{0} Begin: {1}", new string('-', (j + 1) * 4), newScopes[j]);
                            sb.AppendLine();
                            if (caterogy != null)
                            {
                                sb.AppendLine(caterogy);
                            }
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
                        if (caterogy != null)
                        {
                            sb.AppendLine(caterogy);
                        }
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
            _logQueue.Enqueue(logEntry);
        }
        #endregion

        #region Service


        protected CancellationTokenSource _shutdown;
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
            // Stop called without start
            if (_backgroundTask == null || _backgroundTask.IsCompleted)
            {
                return;
            }

            // Signal cancellation to the executing method
            _shutdown.Cancel();

            // Wait until the task completes or the stop token triggers

            await Task.WhenAny(_backgroundTask, Task.Delay(5000, cancellationToken));

            // Throw if cancellation triggered
            cancellationToken.ThrowIfCancellationRequested();
        }

        /// <summary>
        /// Processing logs queue
        /// </summary>
        /// <returns></returns>
        protected async Task ExecuteAsync()
        {
            while (!_shutdown.IsCancellationRequested)
            {
                await WriteFromQueueAsync();
                try
                {
                    await Task.Delay(_bufferTime, _shutdown.Token);
                }
                catch (TaskCanceledException) { }
            }

            try
            {
                await WriteFromQueueAsync(true);
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
