using System.Buffers.Binary;
using TwdSaveEditor.Core.Binary.MetaStream;
using TwdSaveEditor.Core.Binary.SaveGames;
using TwdSaveEditor.Core.Model;

namespace TwdSaveEditor.Core.Binary.Tests.SaveGames;

public class MalformedSaveGameTests
{
    private const int ScriptLengthOffset = 4;

    [Fact]
    public void ScriptLongerThanTheFile_IsRejected()
    {
        var save = new SaveGameFile { LuaDoFile = "logic_game.lua", Agents = [], RuntimePropertyNames = [], EnabledDynamicSets = [] };
        var content = MetaStreamCodec.Read(SaveGameCodec.Write(save));
        BinaryPrimitives.WriteInt32LittleEndian(content.Default.AsSpan(ScriptLengthOffset), 0x7FFFFF00);

        Assert.Throws<InvalidDataException>(() => SaveGameCodec.Read(MetaStreamCodec.Write(content)));
    }
}
