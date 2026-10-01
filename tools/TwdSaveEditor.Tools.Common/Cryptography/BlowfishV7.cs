using System.Buffers.Binary;

namespace TwdSaveEditor.Tools.Common.Cryptography;

public sealed class BlowfishV7
{
    private const int BlockSize = 8;

    private readonly BlowfishKeySchedule _schedule;

    public BlowfishV7(ReadOnlySpan<byte> key)
    {
        _schedule = new BlowfishKeySchedule(key, swapV7Entry: true);
    }

    public uint[] P => _schedule.P;
    public uint[][] S => _schedule.S;

    public byte[] DecryptData(ReadOnlySpan<byte> data)
    {
        var result = data.ToArray();
        Decrypt(result);
        return result;
    }

    public void Decrypt(Span<byte> data)
    {
        for (var offset = 0; offset + BlockSize <= data.Length; offset += BlockSize)
        {
            var block = data.Slice(offset, BlockSize);
            var left = BinaryPrimitives.ReadUInt32LittleEndian(block);
            var right = BinaryPrimitives.ReadUInt32LittleEndian(block[4..]);
            Decipher(ref left, ref right);
            BinaryPrimitives.WriteUInt32LittleEndian(block, left);
            BinaryPrimitives.WriteUInt32LittleEndian(block[4..], right);
        }
    }

    private void Decipher(ref uint left, ref uint right)
    {
        var p = _schedule.P;
        for (var i = BlowfishKeySchedule.Rounds + 1; i > 1; i--)
        {
            left ^= i switch
            {
                4 => p[2],
                3 => p[1],
                2 => p[4],
                _ => p[i],
            };
            right ^= _schedule.F(left);
            (left, right) = (right, left);
        }

        (left, right) = (right, left);
        right ^= p[3];
        left ^= p[0];
    }
}
