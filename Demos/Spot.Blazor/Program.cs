using System.Runtime.InteropServices.JavaScript;
using Gondwana.Demos.Spot;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

var audioModuleUrl = new Uri(
    new Uri(builder.HostEnvironment.BaseAddress),
    "gondwana-audio.js").AbsoluteUri;
await JSHost.ImportAsync("gondwana-audio", audioModuleUrl);

await builder.Build().RunAsync();
