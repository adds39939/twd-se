using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.UI.Configuration;
using TwdSaveEditor.UI.Services;

namespace TwdSaveEditor.UI.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTwdSaveEditorUI(AppInfo appInfo)
        {
            services.AddSingleton(appInfo);
            services.AddScoped<IFileSystemService, FileSystemService>();
            services.AddScoped<SaveBackupService>();
            services.AddScoped<SaveEditorService>();

            return services;
        }
    }
}
