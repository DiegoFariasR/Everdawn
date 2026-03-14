using System;
using System.Net.Http;
using BattleSandbox.Web;
using GameCore.Scenarios;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddScoped(_ => http);

// Load game content from the wwwroot/GameData/Base static assets served by this host.
// HttpContentSource reads content-index.json to discover available files, then pre-fetches
// each one via HTTP. No repository-relative path math is performed at runtime.
var contentSource = await HttpContentSource.LoadAsync(http, "GameData/Base");

// BattleSandboxEngine is the presentation layer's single entry point into GameCore.
// It owns content loading, scenario resolution, and session lifecycle.
builder.Services.AddSingleton<IBattleSandboxEngine>(new BattleSandboxEngine(contentSource));

await builder.Build().RunAsync();
