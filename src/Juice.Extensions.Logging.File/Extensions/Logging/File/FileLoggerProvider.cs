using System.Collections.Concurrent;
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
            }, entry.State);

            #endregion
            GetFileLogger(serviceIdObj, serviceDescription).Write(log);
        }

        public override void ScopeStarted<TState>(string category, TState state, IExternalScopeProvider? scopeProvider)
        {
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

        public override void ScopeDisposed<TState>(string category, TState state, IExternalScopeProvider? scopeProvider)
        {
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

        private ConcurrentDictionary<Guid, FileLogger> _loggers = new ConcurrentDictionary<Guid, FileLogger>();

        private FileLogger GetFileLogger(object? serviceIdObj, string? serviceDescription)
        {
            var serviceId = serviceIdObj != null ?
               (serviceIdObj.GetType() == typeof(Guid) ?
                   (Guid)serviceIdObj
                   : Guid.TryParse(serviceIdObj.ToString(), out var id) ? id : Guid.Empty)
                   : Guid.Empty;

            return _loggers.GetOrAdd(serviceId, (id) =>
            {
                var logger = new FileLogger(Path.Combine(Options.Directory!, serviceDescription ?? Options.GeneralName ?? "General"),
                                       Options, !string.IsNullOrEmpty(serviceDescription));
                logger.StartAsync().Wait();
                return logger;
            });
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
