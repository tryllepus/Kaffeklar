using KaffeklarWasmClient;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using MudBlazor.Services;
using Serilog;


var builder = WebAssemblyHostBuilder.CreateDefault(args);

// Indlæs appsettings.json
var configuration = builder.Configuration;
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Hent API URL'en fra appsettings.json
var apiBaseUrl = configuration["ApiSettings:BaseUrl"];

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(apiBaseUrl) });
builder.Services.AddMudServices();

// Log setup
var serviceProvider = builder.Services.BuildServiceProvider();
var jsRuntime = serviceProvider.GetRequiredService<IJSRuntime>();

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .WriteTo.BrowserConsole(jsRuntime: jsRuntime)
    .CreateLogger();

await builder.Build().RunAsync();

