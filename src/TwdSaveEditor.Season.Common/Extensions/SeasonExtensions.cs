using TwdSaveEditor.Core.Model;
using TwdSaveEditor.Season.Common.Abstractions;

namespace TwdSaveEditor.Season.Common.Extensions;

public static class SeasonExtensions
{
    extension(ISeasonHandler handler)
    {
        public SaveSlot CreateSave(string fileName, int episode)
        {
            var slot = handler.CreateBlankSave(fileName, handler.GetEpisodeId(episode));
            handler.PopulateChoices(slot, episode);
            return slot;
        }
    }

    extension(ISeasonRegistry registry)
    {
        public SaveSlot CreateSave(string seasonKey, int episode, string fileName)
        {
            var handler = registry.Get(seasonKey)
                ?? throw new ArgumentException($"Unknown season: {seasonKey}");
            return handler.CreateSave(fileName, episode);
        }

        public IReadOnlyList<string> GetScenes(string episodeId)
        {
            foreach (var handler in registry.All)
            {
                var scenes = handler.GetScenes(episodeId);
                if (scenes.Count > 0)
                    return scenes;
            }

            return [];
        }
    }
}
