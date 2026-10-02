namespace TwdSaveEditor.Tools.ExtractChapters.Model;

public sealed record SceneScript(string Name, string? Scene, IReadOnlyList<string> Dialogs, IReadOnlyList<string> LoadedScripts);
