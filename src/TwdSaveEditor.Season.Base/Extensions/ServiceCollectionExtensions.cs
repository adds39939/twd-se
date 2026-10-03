using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Season.Base.DialogLog;
using TwdSaveEditor.Season.Base.Story;

namespace TwdSaveEditor.Season.Base.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddStorySeasonServices()
        {
            services.AddSingleton<IDialogLogCompanions, DialogLogCompanions>();
            services.AddSingleton<IStorySaveFactory, StorySaveFactory>();

            return services;
        }
    }
}
