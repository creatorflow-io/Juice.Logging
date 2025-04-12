namespace Juice.Extensions.Logging.File
{
    public class FileLoggerOptions
    {
        public string? GeneralName { get; set; }
        public string? Directory { get; set; }
        public int RetainPolicyFileCount { get; set; } = 50;
        /// <summary>
        /// The expected size of the log file in bytes. The default is 5 MB.
        /// </summary>
        public int MaxFileSize = 5 * 1024 * 1024;
        public TimeSpan BufferTime { get; set; } = TimeSpan.FromSeconds(5);
        public bool ForkJobLog { get; set; } = true;
        public bool IncludeScopes { get; set; }
        /// <summary>
        /// Include categories in named file
        /// </summary>
        public bool IncludeCategories { get; set; }
    }
}
