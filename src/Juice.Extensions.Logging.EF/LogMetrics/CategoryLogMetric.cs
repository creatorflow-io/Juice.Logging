namespace Juice.Extensions.Logging.EF.LogMetrics
{
    public class CategoryLogMetric : LogMetric, IEquatable<CategoryLogMetric>
    {
        public CategoryLogMetric(string category, uint errCount, uint wrnCount, uint criCount,
            uint dbgCount, uint infCount,
            DateTimeOffset timestamp) :
            base(errCount, wrnCount, criCount, dbgCount, infCount, timestamp)
        {
            Category = category;
        }
        public string Category { get; set; }

        public bool Equals(CategoryLogMetric? other)
            => Category.Equals(other?.Category) && Timestamp.Equals(other?.Timestamp);
    }
}
