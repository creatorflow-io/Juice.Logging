using Juice.EF;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Logging.EF.LogEntries
{
    public class LogAdminDbContext : DbContext, ISchemaDbContext
    {
        public string? Schema { get; private set; }
        public LogAdminDbContext(DbContextOptions<LogAdminDbContext> options) : base(options)
        {
        }

        public void ConfigureServices(IServiceProvider serviceProvider)
        {
            var dbOptions = serviceProvider.GetService<DbOptions<LogDbContext>>();
            Schema = dbOptions?.Schema;
        }

        public DbSet<LogEntry> Logs { get; set; }

        protected override void OnModelCreating(ModelBuilder builder)
        {
            builder.Entity<LogEntry>(ConfigureLogEntry);
        }

        private void ConfigureLogEntry(EntityTypeBuilder<LogEntry> builder)
        {
            builder.ToTable("Logs", Schema);
            builder.HasKey(x => x.Id);
            builder.Property(x => x.Id).ValueGeneratedOnAdd();
            builder.Property(x => x.ServiceId);
            builder.Property(x => x.Operation).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.TraceId).HasMaxLength(LengthConstants.IdentityLength);
            builder.Property(x => x.Category).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.Message).HasMaxLength(LengthConstants.ShortDescriptionLength);

            builder.HasIndex(x => x.TraceId);
            builder.HasIndex(x => x.ServiceId);
            builder.HasIndex(x => new { x.Level, x.Timestamp, x.Operation });
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            throw new NotSupportedException("This context is read-only");
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            throw new NotSupportedException("This context is read-only");
        }
    }

    /// <summary>
    /// This factory is used to create a scoped DbContext instance for pooled db
    /// </summary>
    public class LogAdminDbContextScopedFactory : IDbContextFactory<LogAdminDbContext>
    {

        private readonly IDbContextFactory<LogAdminDbContext> _pooledFactory;
        private readonly IServiceProvider _serviceProvider;

        public LogAdminDbContextScopedFactory(
            IDbContextFactory<LogAdminDbContext> pooledFactory,
            IServiceProvider serviceProvider)
        {
            _pooledFactory = pooledFactory;
            _serviceProvider = serviceProvider;
        }

        public LogAdminDbContext CreateDbContext()
        {
            var context = _pooledFactory.CreateDbContext();
            context.ConfigureServices(_serviceProvider);
            return context;
        }
    }
}
