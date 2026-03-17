using TwdSaveEditor.Core.Hashing;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.GameData.Seasons;

public class MichonneHandler : ISeasonHandler
{
    public string SeasonKey => "michonne";
    public string FilePrefix => "wdm_";
    public bool UsesEventLog => true;
    public bool UsesChoiceStats => false;

    public string GetEpisodeId(int episode) => $"Michonne10{episode}";

    public SaveSlot CreateBlankSave(string fileName, string episodeId)
        => SaveSlotFactory.CreateBlankS3Michonne(fileName, episodeId);

    public IChoiceAccessor? CreateChoiceAccessor(SaveSlot slot)
        => new EventLogAccessor(slot);

    public void PopulateChoices(SaveSlot slot, int episode)
    {
        var choices = ChoiceDatabase.ForSeason(SeasonKey)
            .Where(c => c.Episode <= episode)
            .ToList();

        if (choices.Count == 0)
            return;

        var eventEntries = new List<EventLogEntry>();
        uint seqIdx = 0;

        foreach (var c in choices)
        {
            var guid = ChoiceNodeMapping.GetMichonneGuid(c.ChoiceKey, c.Options[0].Value);
            if (guid == null)
                continue;

            var nodeHash = TelltaleHash.ComputeCrc64("{" + guid + "}");

            eventEntries.Add(new EventLogEntry
            {
                EventTypeHash = EventLogEntry.EventTypes.ExecutingDialogNode,
                NodeHash = nodeHash,
                ValueType = 1,
                ExtraFlag = 0,
                SequenceIndex = seqIdx++,
                Trailing = 0,
            });
        }

        slot.PendingEventLogEntries = eventEntries;
    }

    public bool CanHandle(string fileName)
    {
        var name = Path.GetFileName(fileName).ToLowerInvariant();
        if (name.StartsWith('_')) name = name[1..];
        return name.StartsWith("wdm_");
    }
}
