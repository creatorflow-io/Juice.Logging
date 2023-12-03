using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.Metrics
{
    internal class LogMetric
    {
        private uint _warningCount;
        private uint _errorCount;
        private uint _criticalCount;

        public LogMetric(LogLevel level)
        {
            Track(level);
        }

        public LogMetric(uint warningCount, uint errorCount, uint criticalCount)
        {
            _warningCount = warningCount;
            _errorCount = errorCount;
            _criticalCount = criticalCount;
        }

        public uint WarningCount => _warningCount;
        public uint ErrorCount => _errorCount;
        public uint CriticalCount => _criticalCount;
        public LogMetric Track(LogLevel logLevel)
        {
            switch (logLevel)
            {
                case LogLevel.Warning:
                    Interlocked.Increment(ref _warningCount);
                    break;
                case LogLevel.Error:
                    Interlocked.Increment(ref _errorCount);
                    break;
                case LogLevel.Critical:
                    Interlocked.Increment(ref _criticalCount);
                    break;
                default:
                    break;
            }
            return this;
        }

        public LogMetric GetValue()
        {
            return new LogMetric(Interlocked.Exchange(ref _warningCount, 0), Interlocked.Exchange(ref _errorCount, 0), Interlocked.Exchange(ref _criticalCount, 0));
        }
    }
}
