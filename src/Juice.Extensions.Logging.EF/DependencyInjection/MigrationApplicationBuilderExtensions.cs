using Juice.Extensions.Logging.EF.LogEntries;
using Juice.Extensions.Logging.EF.LogMetrics;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Logging.EF.DependencyInjection
{
    public static class MigrationApplicationBuilderExtensions
    {
        public static async Task MigrateLogDbAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LogDbContext>();
            await context.Database.MigrateAsync();
        }

        public static async Task MigrateLogMetricsDbAsync(this IApplicationBuilder app)
        {
            using var scope = app.ApplicationServices.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<LogMetricsDbContext>();
            await context.Database.MigrateAsync();
        }
    }
}
