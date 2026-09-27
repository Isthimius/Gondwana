using System.Runtime.InteropServices.JavaScript;
using Gondwana.Demos.Spot;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

await JSHost.ImportAsync("gondwana-audio", "./gondwana-audio.js");

await builder.Build().RunAsync();
