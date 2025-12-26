using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using PrintControl.Agent.Models;
using PrintControl.Agent.Services;

var settings = new HostApplicationBuilderSettings
{
    Args = args,
    ContentRootPath = AppContext.BaseDirectory
};

var builder = Host.CreateApplicationBuilder(settings);

builder.Services.Configure<PrintControlOptions>(builder.Configuration.GetSection("PrintControl"));

builder.Services.AddSingleton<DiskQueue>();

builder.Services.AddHttpClient("host", (sp, client) =>
{
    var options = sp.GetRequiredService<IOptions<PrintControlOptions>>().Value;
    client.BaseAddress = new Uri(options.HostUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddHostedService<PrintWatcherService>();

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "PrintControl.Agent";
});

var host = builder.Build();
await host.RunAsync();
