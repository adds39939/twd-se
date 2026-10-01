using TwdSaveEditor.Tools.Common.MetaStreams;

namespace TwdSaveEditor.Tools.Common.Meta;

public sealed record MetaDocument(MetaStreamSections Sections, MetaNode? Root, string? Error, long ErrorOffset, int TrailingBytes);
