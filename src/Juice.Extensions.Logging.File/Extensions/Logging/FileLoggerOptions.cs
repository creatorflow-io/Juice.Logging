namespace Juice.Extensions.Logging.File
{
    public class FileLoggerOptions
    {
        public string? GeneralName { get; set; }
        public string? Directory { get; set; }
        public int RetainPolicyFileCount { get; set; } = 50;
        public int MaxFileSize = 5 * 1024 * 1024;
        public TimeSpan BufferTime { get; set; } = TimeSpan.FromSeconds(5);
        public bool ForkJobLog { get; set; } = true;
        public bool IncludeScopes { get; set; }
    }
}
