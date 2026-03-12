using BattleSandbox.Web;
using GameCore.Content;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Configure the content root explicitly for this host.
// BattleSandbox.Web output is at bin/{Configuration}/{TFM}/ when run locally;
// GameData/Base/ is four directories above that output path.
// The host (Program.cs) owns this decision — no adapter walks the file system at runtime.
var contentRoot = Path.GetFullPath(
    Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "GameData", "Base"));
builder.Services.AddSingleton<IContentSource>(SandboxContentSource.Create(contentRoot));

await builder.Build().RunAsync();
