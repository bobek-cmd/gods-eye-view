namespace DualSenseBridge.Core.DualSense;

/// <summary>
/// Standard CRC-32 (IEEE 802.3, reflected, polynomial 0xEDB88320) as used by
/// the DualSense for every Bluetooth input, output and feature report.
/// The controller prepends a one byte "seed" (0xA1 input, 0xA2 output,
/// 0xA3 feature) that is not part of the transmitted report.
/// </summary>
public static class Crc32
{
    private static readonly uint[] Table = BuildTable();

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            uint c = i;
            for (int k = 0; k < 8; k++)
            {
                c = (c & 1) != 0 ? 0xEDB88320u ^ (c >> 1) : c >> 1;
            }
            table[i] = c;
        }
        return table;
    }

    public static uint Compute(ReadOnlySpan<byte> data) => ~Update(0xFFFFFFFFu, data);

    public static uint ComputeWithSeed(byte seed, ReadOnlySpan<byte> data)
    {
        uint crc = Update(0xFFFFFFFFu, stackalloc byte[] { seed });
        return ~Update(crc, data);
    }

    private static uint Update(uint crc, ReadOnlySpan<byte> data)
    {
        foreach (byte b in data)
        {
            crc = Table[(crc ^ b) & 0xFF] ^ (crc >> 8);
        }
        return crc;
    }
}
