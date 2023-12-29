using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Juice.Extensions.Logging.EF.LogEntries;
using Juice.MultiTenant;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging.Grpc.Server
{
    public class LogService : LogWriter.LogWriterBase
    {
        private LogDbContext _dbContext;
        private ITenant? _tenantInfo;

        public LogService(LogDbContext dbContext, ITenant? tenant = null)
        {
            _dbContext = dbContext;
            _tenantInfo = tenant;
        }

        public override async Task<Empty> Log(LogEntries request, ServerCallContext context)
        {
            var logEntries = request.Entries.Select(x => new LogEntry(
                                x.ServiceId != null
                                    ? Guid.TryParse(x.ServiceId, out var serviceId)
                                        ? serviceId : default(Guid?)
                                        : default(Guid?),
                                x.TraceId,
                                x.Operation,
                                x.Category,
                                x.Message,
                                (LogLevel)x.Level,
                                x.Exception,
                                x.TenantId ?? _tenantInfo?.Id ?? "",
                                x.Timestamp.ToDateTimeOffset()
                                ));

            if (logEntries.Any())
            {
                await _dbContext.Logs.AddRangeAsync(logEntries);
                await _dbContext.SaveChangesAsync();
            }

            return new Empty();
        }
    }
}
