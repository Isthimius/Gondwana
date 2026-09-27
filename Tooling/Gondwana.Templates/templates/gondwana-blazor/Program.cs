using System.Runtime.InteropServices.JavaScript;
using Gondwana;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MyGame;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// JSHost resolves relative module paths from _framework, not from the app base href.
// Build an absolute same-origin URL so root and sub-path deployments both work.
var audioModuleUrl = new Uri(
    new Uri(builder.HostEnvironment.BaseAddress),
    "gondwana-audio.js").AbsoluteUri;
await JSHost.ImportAsync("gondwana-audio", audioModuleUrl);
Engine.Instance.UseBrowserAudio();

await builder.Build().RunAsync();
