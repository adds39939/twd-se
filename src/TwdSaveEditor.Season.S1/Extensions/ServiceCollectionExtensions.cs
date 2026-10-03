using Microsoft.Extensions.DependencyInjection;
using TwdSaveEditor.Season.Common.Abstractions;
using TwdSaveEditor.Season.S1.Handlers;
using TwdSaveEditor.Season.S1.Inventory;
using TwdSaveEditor.Season.S1.Saves;

namespace TwdSaveEditor.Season.S1.Extensions;

public static class ServiceCollectionExtensions
{
    extension(IServiceCollection services)
    {
        public IServiceCollection AddSeason1()
        {
            services.AddSingleton<IS1CheckpointBuilder, S1CheckpointBuilder>();
            services.AddSingleton<IS1ResumePoint, S1ResumePoint>();
            services.AddSingleton<IS1Inventory, S1Inventory>();
            services.AddSingleton<IS1CheckpointRefresher, S1CheckpointRefresher>();
            services.AddSingleton<IS1SaveFactory, S1SaveFactory>();
            services.AddSingleton<ISeasonHandler, S1Handler>();
            services.AddSingleton<ISeasonHandler, S1_400DaysHandler>();

            return services;
        }
    }
}
