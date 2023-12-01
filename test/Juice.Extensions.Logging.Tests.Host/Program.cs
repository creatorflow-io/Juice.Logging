using Juice.Extensions.Logging;
using Juice.Extensions.Logging.Tests.Host;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorPages();
builder.Services.AddSignalR();

builder.Services.AddHostedService<LogService>();
builder.Logging.AddFileLogger(builder.Configuration.GetSection("Logging:File"));
builder.Logging.AddSignalRLogger(builder.Configuration.GetSection("Logging:SignalR"));

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthorization();

app.MapRazorPages();

app.MapHub<LogHub>("/loghub");

app.Run();
