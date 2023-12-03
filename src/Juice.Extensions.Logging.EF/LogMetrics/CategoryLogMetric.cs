namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class CategoryLogMetric
    {
        public CategoryLogMetric(string category, uint errors, uint warnings, uint criticals, DateTimeOffset timestamp)
        {
            Category = category;
            Timestamp = timestamp;

            Errors = errors;
            Warnings = warnings;
            Criticals = criticals;
        }
        public string Category { get; set; }
        public DateTimeOffset Timestamp { get; private set; }

        public uint Errors { get; private set; }
        public uint Warnings { get; private set; }
        public uint Criticals { get; private set; }
    }
}
