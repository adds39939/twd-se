namespace TwdSaveEditor.Tools.Common.MetaStreams;

public sealed record MetaStreamSections(uint Magic, List<VersionEntry> VersionEntries, byte[] Default, byte[] Debug, byte[] Async);
