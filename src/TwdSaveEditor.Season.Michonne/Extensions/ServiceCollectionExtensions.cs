using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Michonne.Handlers;

namespace TwdSaveEditor.Season.Michonne.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddMichonne()
        {
            services.AddSingleton<ISeasonHandler, MichonneHandler>();

            return services;
        }
    }
}
