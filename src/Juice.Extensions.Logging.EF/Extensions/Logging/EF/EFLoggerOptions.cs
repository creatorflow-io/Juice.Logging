using Juice.Extensions.Logging.File;

namespace Juice.Extensions.Logging.EF
{
    public class EFLoggerOptions : FileLoggerOptions
    {
        public bool Disabled { get; set; }
    }
}
