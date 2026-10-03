using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.S3.Handlers;

namespace TwdSaveEditor.Season.S3.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSeason3()
        {
            services.AddSingleton<ISeasonHandler, S3Handler>();

            return services;
        }
    }
}
