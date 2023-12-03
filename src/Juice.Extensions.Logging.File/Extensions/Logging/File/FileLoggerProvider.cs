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

            #region Check job scope to fork new log file for this job
            if (Options.ForkJobLog && (log.Scopes?.Any(s => s.Properties != null && s.Properties.ContainsKey("TraceId")) ?? false))
            {
                var traceId = log.Scopes.Last(s => s.Properties != null
                       && s.Properties.ContainsKey("TraceId")).Properties!["TraceId"]?.ToString();
                var operation = log.Scopes.LastOrDefault(s => s.Properties != null && s.Properties.ContainsKey("Operation"))?
                        .Properties?["Operation"]?.ToString();

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
                if (!string.IsNullOrEmpty(fileName))
                {
                    log.ForkNewFile(fileName);
                }

                var stateScope = log.Scopes.LastOrDefault(s => s.Properties != null
                                   && s.Properties.ContainsKey("OperationState"));
                var state = stateScope?.Properties?["OperationState"]?.ToString() ?? default;
                log.SetState(state);
            }
            #endregion

            GetFileLogger(log).Write(log);
        }

        private Dictionary<Guid, FileLogger> _loggers = new Dictionary<Guid, FileLogger>();
        private FileLogger GetFileLogger(LogEntry entry)
        {
            var scope = entry.Scopes?.LastOrDefault(s => s.Properties != null
                && s.Properties.ContainsKey("ServiceId")
                && s.Properties.ContainsKey("ServiceDescription"));

            var serviceIdObj = scope?.Properties?["ServiceId"];
            var serviceId = serviceIdObj != null ?
                (serviceIdObj.GetType() == typeof(Guid) ?
                    (Guid)serviceIdObj
                    : Guid.TryParse(serviceIdObj.ToString(), out var id) ? id : Guid.Empty)
                    : Guid.Empty;

            if (!_loggers.ContainsKey(serviceId))
            {
                var serviceDescription = scope?.Properties?["ServiceDescription"]?.ToString();

                _loggers.Add(serviceId, new FileLogger(Path.Combine(Options.Directory!, serviceDescription ?? Options.GeneralName ?? "General"),
                    Options));
                _loggers[serviceId].StartAsync().Wait();
            }

            return _loggers[serviceId];
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
