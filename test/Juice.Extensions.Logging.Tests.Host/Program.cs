using Juice.Extensions.Logging;
using Juice.Extensions.Logging.EF.DependencyInjection;
using Juice.Extensions.Logging.SignalR;
using Juice.Extensions.Logging.Tests.Host;
using Juice.MultiTenant;
using Juice.MultiTenant.EF;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR();
builder.Services.AddGrpc();
builder.Services.AddHttpContextAccessor();
builder.Services.AddGrpcLogServices();

builder.Services.AddHostedService<LogService>();
builder.Services.AddSingleton<LogClient>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<LogClient>());
builder.Services.AddSingleton<ILogClient>(sp => sp.GetRequiredService<LogClient>());

builder.Logging.AddFileLogger(builder.Configuration.GetSection("Logging:File"));
builder.Logging.AddSignalRLogger(builder.Configuration.GetSection("Logging:SignalR"));
builder.Logging.AddDbLogger(builder.Configuration.GetSection("Logging:Db"), builder.Configuration);
builder.Logging.AddMetricsLogger(builder.Configuration.GetSection("Logging:Metrics"), builder.Configuration);

ConfigureMultiTenant(builder);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();


app.UseRouting();

app.UseMultiTenant();
app.UseStaticFiles();

app.MapRazorPages();

app.MapHub<LogHub>("/loghub");

app.MapGrpcLogServices();

await app.MigrateLogDbAsync();

await app.MigrateLogMetricsDbAsync();

app.Run();


static void ConfigureMultiTenant(WebApplicationBuilder builder)
{
    var tenantAuthority = builder.Configuration.GetSection("OpenIdConnect:TenantAuthority").Value;
    builder.Services
    .AddMultiTenant(options =>
    {
        options.IgnoredIdentifiers.Add("css");
        options.IgnoredIdentifiers.Add("js");
        options.IgnoredIdentifiers.Add("lib");
        options.IgnoredIdentifiers.Add("images");
        options.IgnoredIdentifiers.Add("favicon.ico");
        options.IgnoredIdentifiers.Add("loghub");
    })
    .ConfigureTenantEFDirectly(builder.Configuration, options =>
    {
        options.DatabaseProvider = "PostgreSQL";
        options.ConnectionName = "PostgreConnection";
        options.Schema = "App";
    }, builder.Environment.EnvironmentName)
    .WithBasePathStrategy(options => options.RebaseAspNetCorePathBase = true)
    ;

}
