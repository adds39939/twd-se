using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Core.Binary.Bundles;
using TwdSaveEditor.Core.Database;
using TwdSaveEditor.Core.Serialization;
using TwdSaveEditor.Season.Base.Extensions;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.Common.Services;
using TwdSaveEditor.Season.Michonne.Extensions;
using TwdSaveEditor.Season.S1.Extensions;
using TwdSaveEditor.Season.S2.Extensions;
using TwdSaveEditor.Season.S3.Extensions;
using TwdSaveEditor.Season.S4.Extensions;

namespace TwdSaveEditor.Bootstrap.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddTwdSaveEditorServices()
        {
            services.AddSingleton<ISaveBundleSerializer, SaveBundleSerializer>();

            services.AddStorySeasonServices();
            services.AddSeason1();
            services.AddSeason2();
            services.AddMichonne();
            services.AddSeason3();
            services.AddSeason4();

            services.AddSingleton<ISeasonRegistry, SeasonRegistry>();
            services.AddSingleton<IBackupFileResolver, BackupFileResolver>();

            services.AddSingleton(sp =>
            {
                var seasons = sp.GetRequiredService<ISeasonRegistry>().All;
                return PropertyNameDb.CreateDefault(
                [
                    .. seasons.SelectMany(season => season.Choices).Select(choice => choice.ChoiceKey),
                    .. seasons.OfType<IPropertyNameProvider>().SelectMany(provider => provider.PropertyNames),
                ]);
            });

            return services;
        }
    }
}
