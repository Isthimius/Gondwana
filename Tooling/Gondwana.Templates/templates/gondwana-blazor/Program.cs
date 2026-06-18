using System.Runtime.InteropServices.JavaScript;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MyGame;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Import the gondwana-audio JS module - use absolute path from root
await JSHost.ImportAsync("gondwana-audio", "/gondwana-audio.js");

await builder.Build().RunAsync();