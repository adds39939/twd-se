using TwdSaveEditor.Tools.Common.Configuration;
using TwdSaveEditor.Tools.DumpMetadata.Metadata;

var saves = Path.Combine(ToolPaths.TestData, "S1");

MetadataDumper.Dump(Path.Combine(saves, "wd1_saveslot1_live.bundle"), "Slot: metadata_slot.p", Console.Out);
MetadataDumper.Dump(Path.Combine(saves, "_wd1_saveslot1_autosave.bundle"), "Autosave: metadata_save.p", Console.Out);
