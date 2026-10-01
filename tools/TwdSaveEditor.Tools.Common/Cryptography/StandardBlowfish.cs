using System.Buffers.Binary;

namespace TwdSaveEditor.Tools.Common.Cryptography;

public sealed class StandardBlowfish
{
    private const int BlockSize = 8;

    private readonly BlowfishKeySchedule _schedule;

    public StandardBlowfish(ReadOnlySpan<byte> key)
    {
        _schedule = new BlowfishKeySchedule(key, swapV7Entry: false);
    }

    public byte[] DecryptEcb(ReadOnlySpan<byte> data)
    {
        var result = data.ToArray();
        for (var offset = 0; offset < result.Length; offset += BlockSize)
        {
            var block = result.AsSpan(offset, BlockSize);
            var left = BinaryPrimitives.ReadUInt32BigEndian(block);
            var right = BinaryPrimitives.ReadUInt32BigEndian(block[4..]);
            Decipher(ref left, ref right);
            BinaryPrimitives.WriteUInt32BigEndian(block, left);
            BinaryPrimitives.WriteUInt32BigEndian(block[4..], right);
        }

        return result;
    }

    private void Decipher(ref uint left, ref uint right)
    {
        var p = _schedule.P;
        for (var i = BlowfishKeySchedule.Rounds + 1; i > 1; i--)
        {
            left ^= p[i];
            right ^= _schedule.F(left);
            (left, right) = (right, left);
        }

        (left, right) = (right, left);
        right ^= p[1];
        left ^= p[0];
    }
}
