using Finbuckle.MultiTenant;
using Finbuckle.MultiTenant.EntityFrameworkCore;
using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Logging.EF.LogEntries
{
    public class LogDbContext : DbContext, ISchemaDbContext, IMultiTenantDbContext
    {
        #region Finbuckle
        public ITenantInfo? TenantInfo { get; internal set; }
        public virtual TenantMismatchMode TenantMismatchMode { get; set; } = TenantMismatchMode.Throw;

        public virtual TenantNotSetMode TenantNotSetMode { get; set; } = TenantNotSetMode.Throw;
        #endregion
        public string? Schema { get; private set; }
        public LogDbContext(IServiceProvider serviceProvider,
            DbContextOptions<LogDbContext> options) : base(options)
        {
            ConfigureServices(serviceProvider);
        }

        public void ConfigureServices(IServiceProvider serviceProvider)
        {
            var dbOptions = serviceProvider.GetService<DbOptions<LogDbContext>>();
            Schema = dbOptions?.Schema;
            TenantInfo = serviceProvider.GetService<ITenantInfo>() ?? new TenantInfo { Id = "" };
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

            builder.IsMultiTenant();

            builder.HasIndex(x => x.TraceId);
            builder.HasIndex(x => x.ServiceId);
            builder.HasIndex(x => new { x.Level, x.Timestamp, x.Operation });
        }

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default(CancellationToken))
        {
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
    }

    /// <summary>
    /// For migrations
    /// </summary>
    public class LogDbContextFactory : IDesignTimeDbContextFactory<LogDbContext>
    {
        public LogDbContext CreateDbContext(string[] args)
        {
            Environment.SetEnvironmentVariable("ASPNETCORE_ENVIRONMENT", "Development");
            var resolver = new DependencyResolver
            {
                CurrentDirectory = AppContext.BaseDirectory
            };

            resolver.ConfigureServices(services =>
            {

                // Register DbContext class
                var configService = services.BuildServiceProvider().GetRequiredService<IConfigurationService>();

                var configuration = configService.GetConfiguration(args);

                var provider = configuration.GetSection("Provider").Get<string>() ?? "SqlServer";
                var connectionName =
                    provider switch
                    {
                        "PostgreSQL" => "PostgreConnection",
                        "SqlServer" => "SqlServerConnection",
                        _ => throw new NotSupportedException($"Unsupported provider: {provider}")
                    }
                ;
                var connectionString = configuration.GetConnectionString(connectionName);

                services.AddScoped(p =>
                {
                    var options = new DbOptions<LogDbContext> { Schema = "App" };
                    return options;
                });

                services.AddDbContext<LogDbContext>(options =>
                {
                    switch (provider)
                    {
                        case "PostgreSQL":
                            AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true);

                            options.UseNpgsql(
                               connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Extensions.Logging.EF.PostgreSQL");
                                });
                            break;

                        case "SqlServer":

                            options.UseSqlServer(
                                connectionString,
                                x =>
                                {
                                    x.MigrationsAssembly("Juice.Extensions.Logging.EF.SqlServer");
                                });
                            break;
                        default:
                            throw new NotSupportedException($"Unsupported provider: {provider}");
                    }

                });

            });

            var dbContext = resolver.ServiceProvider.GetRequiredService<LogDbContext>();
            dbContext.ConfigureServices(resolver.ServiceProvider);
            return dbContext;
        }
    }

    /// <summary>
    /// This factory is used to create a scoped DbContext instance for pooled db
    /// </summary>
    public class LogDbContextScopedFactory : IDbContextFactory<LogDbContext>
    {

        private readonly IDbContextFactory<LogDbContext> _pooledFactory;
        private readonly IServiceProvider _serviceProvider;

        public LogDbContextScopedFactory(
            IDbContextFactory<LogDbContext> pooledFactory,
            IServiceProvider serviceProvider)
        {
            _pooledFactory = pooledFactory;
            _serviceProvider = serviceProvider;
        }

        public LogDbContext CreateDbContext()
        {
            var context = _pooledFactory.CreateDbContext();
            context.ConfigureServices(_serviceProvider);
            return context;
        }
    }
}
