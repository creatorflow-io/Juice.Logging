using Juice.Extensions.Logging.File;

namespace Juice.Extensions.Logging.Grpc
{
    public class GrpcLoggerOptions : FileLoggerOptions
    {
        public string? Endpoint { get; set; }
        public bool Disabled { get; set; }
    }
}
