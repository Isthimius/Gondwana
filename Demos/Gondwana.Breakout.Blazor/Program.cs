using System.Runtime.InteropServices.JavaScript;
using Gondwana;
using Gondwana.Demos.Breakout;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");
builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Use the current Blazor template's app-relative audio module location.
string audioModuleUrl = new Uri(new Uri(builder.HostEnvironment.BaseAddress), "gondwana-audio.js").AbsoluteUri;
await JSHost.ImportAsync("gondwana-audio", audioModuleUrl);
Engine.Instance.UseBrowserAudio();
await builder.Build().RunAsync();
