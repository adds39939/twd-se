using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TwdSaveEditor.Bootstrap.Extensions;
using TwdSaveEditor.UI;
using TwdSaveEditor.UI.Configuration;
using TwdSaveEditor.UI.Extensions;
using TwdSaveEditor.Web.Configuration;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

var http = new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
builder.Services.AddScoped(_ => http);

builder.Services.AddTwdSaveEditorServices();

var appVersion = await new VersionFile(http).ReadVersionAsync();
var appInfo = new AppInfo(appVersion);

builder.Services.AddTwdSaveEditorUI(appInfo);

await builder.Build().RunAsync();
