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

ConfigureServices(builder.Services);

await builder.Build().RunAsync();

static void ConfigureServices(IServiceCollection services)
{
    services.AddTwdSaveEditorServices();
    services.AddTwdSaveEditorUI(new AppInfo(AppVersion.Read()));
}
