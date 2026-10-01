using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.Serialization;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.Michonne.Handlers;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.S2.Handlers;
using TwdSaveEditor.Season.S3.Handlers;
using TwdSaveEditor.Season.S4.Handlers;

namespace TwdSaveEditor.Bootstrap.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTwdSaveEditorServices()
        {
            services.AddSingleton<ISaveBundleSerializer, SaveBundleSerializer>();

            services.AddSingleton<ISeasonHandler, S1Handler>();
            services.AddSingleton<ISeasonHandler, S1_400DaysHandler>();
            services.AddSingleton<ISeasonHandler, S2Handler>();
            services.AddSingleton<ISeasonHandler, MichonneHandler>();
            services.AddSingleton<ISeasonHandler, S3Handler>();
            services.AddSingleton<ISeasonHandler, S4Handler>();
            services.AddSingleton<ISeasonRegistry, SeasonRegistry>();

            services.AddSingleton(sp => PropertyNameDb.CreateDefault(
                sp.GetRequiredService<ISeasonRegistry>().All
                    .SelectMany(season => season.Choices)
                    .Select(choice => choice.ChoiceKey)));

            return services;
        }
    }
}
