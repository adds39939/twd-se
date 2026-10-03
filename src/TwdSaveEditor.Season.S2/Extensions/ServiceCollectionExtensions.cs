using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.S2.Handlers;
using TwdSaveEditor.Season.S2.Inventory;
using TwdSaveEditor.Season.S2.Saves;

namespace TwdSaveEditor.Season.S2.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSeason2()
        {
            services.AddSingleton<IS2CheckpointBuilder, S2CheckpointBuilder>();
            services.AddSingleton<IS2ResumePoint, S2ResumePoint>();
            services.AddSingleton<IS2Inventory, S2Inventory>();
            services.AddSingleton<IS2SaveFactory, S2SaveFactory>();
            services.AddSingleton<ISeasonHandler, S2Handler>();

            return services;
        }
    }
}
