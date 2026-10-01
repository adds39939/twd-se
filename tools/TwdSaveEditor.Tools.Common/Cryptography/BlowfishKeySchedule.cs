using System.Buffers.Binary;

namespace TwdSaveEditor.Tools.Common.Cryptography;

public sealed class BlowfishKeySchedule
{
    public const int Rounds = 16;

    public BlowfishKeySchedule(ReadOnlySpan<byte> key, bool swapV7Entry)
    {
        P = [.. BlowfishTables.P];
        S = [[.. BlowfishTables.S0], [.. BlowfishTables.S1], [.. BlowfishTables.S2], [.. BlowfishTables.S3]];

        if (swapV7Entry)
            S[0][118] = BinaryPrimitives.ReverseEndianness(S[0][118]);

        var keyIndex = 0;
        for (var i = 0; i < Rounds + 2; i++)
        {
            uint word = 0;
            for (var k = 0; k < 4; k++)
            {
                word = (word << 8) | key[keyIndex];
                keyIndex = (keyIndex + 1) % key.Length;
            }

            P[i] ^= word;
        }

        uint left = 0;
        uint right = 0;
        for (var i = 0; i < Rounds + 2; i += 2)
        {
            Encipher(ref left, ref right);
            P[i] = left;
            P[i + 1] = right;
        }

        foreach (var box in S)
        {
            for (var k = 0; k < box.Length; k += 2)
            {
                Encipher(ref left, ref right);
                box[k] = left;
                box[k + 1] = right;
            }
        }
    }

    public uint[] P { get; }
    public uint[][] S { get; }

    public uint F(uint x) =>
        ((S[0][x >> 24] + S[1][(x >> 16) & 0xFF]) ^ S[2][(x >> 8) & 0xFF]) + S[3][x & 0xFF];

    public void Encipher(ref uint left, ref uint right)
    {
        for (var i = 0; i < Rounds; i++)
        {
            left ^= P[i];
            right ^= F(left);
            (left, right) = (right, left);
        }

        (left, right) = (right, left);
        right ^= P[Rounds];
        left ^= P[Rounds + 1];
    }
}
