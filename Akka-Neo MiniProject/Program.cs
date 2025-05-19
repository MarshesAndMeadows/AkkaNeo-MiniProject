using AkkaNeo_MiniProject;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using AkkaNeo_Blazor;
using AkkaNeo_Blazor.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

// Register HTTP client for API calls
builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Register services
builder.Services.AddScoped<Neo4jService>();
builder.Services.AddScoped<AkkaService>();

// Add SignalR client for real-time updates
builder.Services.AddSignalRCore();

await builder.Build().RunAsync();
