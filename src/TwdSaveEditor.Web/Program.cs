using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TwdSaveEditor.Bootstrap.Extensions;
using TwdSaveEditor.UI;
using TwdSaveEditor.UI.Configuration;
using TwdSaveEditor.UI.Extensions;
using TwdSaveEditor.Web;

var builder = WebAssemblyHostBuilder.CreateDefault(args);

builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

builder.Services.AddTwdSaveEditorServices();
builder.Services.AddTwdSaveEditorUI(new AppInfo(BuildInfo.GitHash));

await builder.Build().RunAsync();
