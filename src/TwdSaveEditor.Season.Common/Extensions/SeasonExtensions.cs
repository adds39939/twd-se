using System.Text.RegularExpressions;
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

        public string SaveSlotFileName(int slot) => $"{handler.FilePrefix}saveslot{slot}.bundle";

        public int NextFreeSlot(IEnumerable<string> fileNames)
        {
            var pattern = new Regex($@"^_?{Regex.Escape(handler.FilePrefix)}saveslot(\d+)(?:[._]|$)",
                RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
            var used = new HashSet<int>();
            foreach (var fileName in fileNames)
            {
                if (pattern.Match(fileName) is { Success: true } match && int.TryParse(match.Groups[1].ValueSpan, out var slot))
                {
                    used.Add(slot);
                }
            }

            var next = 1;
            while (used.Contains(next))
            {
                next++;
            }

            return next;
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
    }
}
