using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.S4.Collectibles;
using TwdSaveEditor.Season.S4.Handlers;

namespace TwdSaveEditor.Season.S4.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSeason4()
        {
            services.AddSingleton<IS4Collectibles, S4Collectibles>();
            services.AddSingleton<ISeasonHandler, S4Handler>();

            return services;
        }
    }
}
