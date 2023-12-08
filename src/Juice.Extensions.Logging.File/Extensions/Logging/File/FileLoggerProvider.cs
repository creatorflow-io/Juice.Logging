using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Logging.File
{
    [ProviderAlias("File")]
    internal class FileLoggerProvider : LoggerProvider
    {

        private IOptionsMonitor<FileLoggerOptions> _optionsMonitor;

        public FileLoggerOptions Options => _optionsMonitor.CurrentValue;

        public FileLoggerProvider(IOptionsMonitor<FileLoggerOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
        }


        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
        {
            var log = new LogEntry(DateTimeOffset.Now, entry.Category, formattedMessage, entry.LogLevel, entry.Exception);

            #region Collect log scopes
            scopeProvider?.ForEachScope((value, loggingProps) =>
            {
                if (value is string)
                {
                    log.PushScope(new LogScope { Scope = value.ToString() });
                }
                else if (value is IEnumerable<string> scopes)
                {
                    foreach (var scope in scopes)
                    {
                        log.PushScope(new LogScope { Scope = scope });
                    }
                }
                else if (value is IEnumerable<KeyValuePair<string, object>> props)
                {
                    log.PushScope(new LogScope { Properties = props.ToDictionary(kvp => kvp.Key, kvp => kvp.Value) });
                }
            }, entry.State);

            #endregion

            GetFileLogger(log).Write(log);
        }

        public override void ScopeStarted<TState>(TState state, IExternalScopeProvider? scopeProvider)
        {
            var log = new LogEntry(DateTimeOffset.Now, "", "", LogLevel.Information, default);

            object? serviceIdObj = null;
            string? serviceDescription = null;
            #region Collect log scopes
            scopeProvider?.ForEachScope((value, loggingProps) =>
            {
                if (value is IEnumerable<KeyValuePair<string, object>> props)
                {
                    if (props.Any(p => p.Key == "ServiceId"))
                    {
                        serviceIdObj = props.First(p => p.Key == "ServiceId").Value;
                    }
                    if (props.Any(p => p.Key == "ServiceDescription"))
                    {
                        serviceDescription = props.First(p => p.Key == "ServiceDescription").Value?.ToString();
                    }
                }
            }, state);

            #endregion
            GetFileLogger(serviceIdObj, serviceDescription).BeginScope(state);
        }

        public override void ScopeDisposed<TState>(TState state, IExternalScopeProvider? scopeProvider)
        {
            var log = new LogEntry(DateTimeOffset.Now, "", "", LogLevel.Information, default);

            object? serviceIdObj = null;
            string? serviceDescription = null;
            #region Collect log scopes
            scopeProvider?.ForEachScope((value, loggingProps) =>
            {
                if (value is IEnumerable<KeyValuePair<string, object>> props)
                {
                    if (props.Any(p => p.Key == "ServiceId"))
                    {
                        serviceIdObj = props.First(p => p.Key == "ServiceId").Value;
                    }
                    if (props.Any(p => p.Key == "ServiceDescription"))
                    {
                        serviceDescription = props.First(p => p.Key == "ServiceDescription").Value?.ToString();
                    }
                }
            }, state);

            #endregion
            GetFileLogger(serviceIdObj, serviceDescription).EndScope(state);
        }

        private Dictionary<Guid, FileLogger> _loggers = new Dictionary<Guid, FileLogger>();
        private FileLogger GetFileLogger(LogEntry entry)
        {
            var scope = entry.Scopes?.LastOrDefault(s => s.Properties != null
                && s.Properties.ContainsKey("ServiceId")
                && s.Properties.ContainsKey("ServiceDescription"));

            var serviceIdObj = scope?.Properties?["ServiceId"];

            return GetFileLogger(serviceIdObj, scope?.Properties?["ServiceDescription"]?.ToString());
        }

        private FileLogger GetFileLogger(object? serviceIdObj, string? serviceDescription)
        {
            var serviceId = serviceIdObj != null ?
               (serviceIdObj.GetType() == typeof(Guid) ?
                   (Guid)serviceIdObj
                   : Guid.TryParse(serviceIdObj.ToString(), out var id) ? id : Guid.Empty)
                   : Guid.Empty;
            if (!_loggers.ContainsKey(serviceId))
            {
                _loggers.Add(serviceId, new FileLogger(Path.Combine(Options.Directory!, serviceDescription ?? Options.GeneralName ?? "General"),
                    Options, !string.IsNullOrEmpty(serviceDescription)));
                _loggers[serviceId].StartAsync().Wait();
            }

            return _loggers[serviceId];
        }

        public static string GetForkedFileName(string traceId, string? operation)
        {
            var fileName = traceId;
            if (!string.IsNullOrEmpty(operation))
            {
                if (string.IsNullOrEmpty(fileName))
                {
                    fileName = operation;
                }
                else
                {
                    fileName += " - " + operation;
                }
            }
            return fileName;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                var tasks = new List<Task>();
                foreach (var logger in _loggers.Values)
                {
                    tasks.Add(logger.StopAsync(default));
                }
                if (tasks.Any())
                {
                    Task.WaitAll(tasks.ToArray());
                }
                foreach (var logger in _loggers.Values)
                {
                    logger.Dispose();
                }
            }
            base.Dispose(disposing);
        }
    }
}
