using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using TwdSaveEditor.Core.GameData;
using TwdSaveEditor.Core.GameData.Seasons;
using TwdSaveEditor.Web;
using TwdSaveEditor.Web.Services;

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.RootComponents.Add<HeadOutlet>("head::after");

builder.Services.AddScoped(sp => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });

// Season handlers + registry
builder.Services.AddSingleton<ISeasonHandler, S1Handler>();
builder.Services.AddSingleton<ISeasonHandler, S1_400DaysHandler>();
builder.Services.AddSingleton<ISeasonHandler, S2Handler>();
builder.Services.AddSingleton<ISeasonHandler, S3Handler>();
builder.Services.AddSingleton<ISeasonHandler, S4Handler>();
builder.Services.AddSingleton<ISeasonHandler, MichonneHandler>();
builder.Services.AddSingleton<ISeasonRegistry, SeasonRegistry>();

builder.Services.AddScoped<FileSystemService>();
builder.Services.AddScoped<SaveBackupService>();
builder.Services.AddScoped<SaveEditorService>();

await builder.Build().RunAsync();
