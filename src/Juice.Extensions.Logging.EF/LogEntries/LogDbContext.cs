using Juice.EF;
using Juice.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Juice.Extensions.Logging.EF.LogEntries
{
    public class LogDbContext : DbContext, ISchemaDbContext
    {
        public string? Schema { get; private set; }
        public LogDbContext(DbContextOptions<LogDbContext> options) : base(options)
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
            builder.Property(x => x.ServiceId).IsRequired();
            builder.Property(x => x.Operation).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.TraceId).HasMaxLength(LengthConstants.IdentityLength);
            builder.Property(x => x.Category).HasMaxLength(LengthConstants.NameLength);
            builder.Property(x => x.Message).HasMaxLength(LengthConstants.ShortDescriptionLength);

            builder.HasIndex(x => x.TraceId);
            builder.HasIndex(x => x.ServiceId);
            builder.HasIndex(x => new { x.Level, x.Timestamp, x.Operation });
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
