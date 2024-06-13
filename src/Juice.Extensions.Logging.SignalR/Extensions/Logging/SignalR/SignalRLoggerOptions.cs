using Juice.Extensions.Logging.File;

namespace Juice.Extensions.Logging.SignalR
{
    public class SignalRLoggerOptions : FileLoggerOptions
    {
        public string HubUrl { get; set; }
        public string? LogMethod { get; set; }
        public string? StateMethod { get; set; }
        public new bool IncludeScopes { get; set; } = true;
        public string[] ExcludedScopes { get; set; } = Array.Empty<string>();
        public bool Disabled { get; set; }
    }
}
