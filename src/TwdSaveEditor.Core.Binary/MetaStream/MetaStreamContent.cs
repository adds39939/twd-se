using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.MetaStream;

public sealed record MetaStreamContent(MetaStreamHeader Header, byte[] Default, byte[] Debug, byte[] Async);
