using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.Metrics
{
    internal class LogMetric
    {
        private uint _warningCount;
        private uint _errorCount;
        private uint _criticalCount;
        private uint _infoCount;
        private uint _debugCount;

        public LogMetric(LogLevel level)
        {
            Track(level);
        }

        public LogMetric(uint warningCount, uint errorCount, uint criticalCount, uint infoCount, uint debugCount)
        {
            _warningCount = warningCount;
            _errorCount = errorCount;
            _criticalCount = criticalCount;
            _infoCount = infoCount;
            _debugCount = debugCount;
        }

        public uint WarningCount => _warningCount;
        public uint ErrorCount => _errorCount;
        public uint CriticalCount => _criticalCount;
        public uint InfoCount => _infoCount;
        public uint DebugCount => _debugCount;
        public uint TotalCount => _warningCount + _errorCount + _criticalCount + _infoCount + _debugCount;
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
                case LogLevel.Information:
                    Interlocked.Increment(ref _infoCount);
                    break;
                case LogLevel.Debug:
                    Interlocked.Increment(ref _debugCount);
                    break;
                default:
                    break;
            }
            return this;
        }

        public LogMetric GetValue()
        {
            return new LogMetric(Interlocked.Exchange(ref _warningCount, 0),
                Interlocked.Exchange(ref _errorCount, 0), Interlocked.Exchange(ref _criticalCount, 0),
                Interlocked.Exchange(ref _infoCount, 0), Interlocked.Exchange(ref _debugCount, 0)
                );
        }
    }
}
