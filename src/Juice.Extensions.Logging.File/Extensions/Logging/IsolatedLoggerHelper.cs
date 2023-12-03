using Juice.Extensions.Logging.File;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Juice.Extensions.Logging
{
    public static class IsolatedLoggerHelper
    {

        public static ILogger BuildLogger(string category, FileLoggerOptions options, LogLevel minLevel = LogLevel.Information)
        {
            var services = new ServiceCollection();
            services.AddLogging(builder =>
            {
                ConfigureLogger(builder, category, options);
            });
            return services.BuildServiceProvider().GetRequiredService<ILoggerFactory>().CreateLogger(category);
        }

        public static void ConfigureLogger(ILoggingBuilder builder, string name, FileLoggerOptions loggerOptions, LogLevel minLevel = LogLevel.Information)
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.AddFileLogger(options =>
            {
                options.Directory = loggerOptions.Directory;
                options.RetainPolicyFileCount = loggerOptions.RetainPolicyFileCount;
                options.ForkJobLog = false;
                options.BufferTime = loggerOptions.BufferTime;
                options.GeneralName = loggerOptions.GeneralName ?? name;
            });
            builder.SetMinimumLevel(minLevel);
        }

    }
}
