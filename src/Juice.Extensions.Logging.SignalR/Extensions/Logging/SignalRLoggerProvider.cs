﻿using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Juice.Extensions.Logging.SignalR
{
    [ProviderAlias("SignalR")]
    internal class SignalRLoggerProvider : LoggerProvider
    {
        private IOptionsMonitor<SignalRLoggerOptions> _optionsMonitor;
        public SignalRLoggerOptions Options => _optionsMonitor.CurrentValue;

        public SignalRLoggerProvider(IOptionsMonitor<SignalRLoggerOptions> optionsMonitor)
        {
            _optionsMonitor = optionsMonitor;
        }

        public override void WriteLog<TState>(LogEntry<TState> entry, string formattedMessage, IExternalScopeProvider? scopeProvider)
        {
            Guid? serviceId = default;
            string? jobId = default;
            string? contextual = default;
            string? jobState = default;
            List<string> scopes = new List<string>();

            #region Collect log scopes
            scopeProvider?.ForEachScope((value, loggingProps) =>
            {
                if (value is IEnumerable<KeyValuePair<string, object>> props)
                {
                    if (props.Any(p => p.Key == "ServiceId"))
                    {
                        serviceId = Guid.Parse(props.First(p => p.Key == "ServiceId").Value.ToString()!);
                    }
                    if (props.Any(p => p.Key == "JobId"))
                    {
                        jobId = props.First(p => p.Key == "JobId").Value.ToString();
                    }
                    if (props.Any(p => p.Key == "JobState"))
                    {
                        jobState = props.First(p => p.Key == "JobState").Value.ToString();
                    }
                    if (props.Any(p => p.Key == "Contextual"))
                    {
                        contextual = props.First(p => p.Key == "Contextual").Value.ToString();
                    }
                }
                else if (value is string s)
                {
                    scopes.Add(s);
                }
                else if (value is IEnumerable<string> eScopes)
                {
                    scopes.AddRange(eScopes);
                }
            }, entry.State);

            #endregion

            if (serviceId.HasValue)
            {
                var logger = GetLogger(serviceId.Value);
                if (!string.IsNullOrEmpty(jobState))
                {
                    logger.StateAsync(serviceId.Value, jobId, jobState, formattedMessage).Wait();
                }
                else
                {
                    logger.LoggingAsync(serviceId.Value, jobId, formattedMessage,
                        entry.LogLevel, contextual, scopes.ToArray()).Wait();
                }
            }
        }

        private Dictionary<Guid, SignalRLogger> _loggers = new Dictionary<Guid, SignalRLogger>();

        private SignalRLogger GetLogger(Guid serviceId)
        {

            if (!_loggers.ContainsKey(serviceId))
            {
                try
                {
                    _loggers.Add(serviceId, new SignalRLogger(serviceId.ToString(), Options));
                    _loggers[serviceId].StartAsync().Wait();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Error while initializing SignalRLogger: {ex.Message}");
                    throw new Exception($"Error while initializing SignalRLogger: {ex.Message}", ex);
                }
            }

            return _loggers[serviceId];
        }
    }
}
