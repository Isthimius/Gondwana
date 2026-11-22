using System.Runtime.InteropServices.JavaScript;
using Gondwana.Demos.Slider;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient
{
    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
});

// Browser audio initializes before any audio resources are loaded.
string audioModuleUrl = new Uri(
    new Uri(builder.HostEnvironment.BaseAddress), "gondwana-audio.js").AbsoluteUri;
await JSHost.ImportAsync("gondwana-audio", audioModuleUrl);
await builder.Build().RunAsync();
