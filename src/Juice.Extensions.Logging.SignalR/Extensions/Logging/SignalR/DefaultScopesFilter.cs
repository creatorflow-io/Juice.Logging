using Microsoft.Extensions.Options;

namespace Juice.Extensions.Logging.SignalR
{
    internal class DefaultScopesFilter : IScopesFilter
    {
        private string[] _excludedScopes = new string[0];
        public DefaultScopesFilter(IOptionsMonitor<SignalRLoggerOptions> optionsMonitor)
        {
            optionsMonitor.OnChange((options, _) =>
            {
                InitExcludedScopes(options.ExcludedScopes);
            });
            InitExcludedScopes(optionsMonitor.CurrentValue.ExcludedScopes);
        }


        private void InitExcludedScopes(string[] excludedScopes)
        {
            var scopes = new List<string>(excludedScopes);
            scopes.AddRange(new string[] { "ServiceId", "ServiceDescription",
                "TraceId", "OperationState", "Contextual" });
            _excludedScopes = scopes.Distinct().ToArray();
        }

        public bool IsIncluded(string scope)
        {
            return !_excludedScopes.Contains(scope);
        }

    }
}
