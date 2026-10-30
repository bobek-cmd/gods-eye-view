using System.Text;
using DualSenseBridge.Core.DualSense;

namespace DualSenseBridge.Tests;

public class Crc32Tests
{
    [Fact]
    public void MatchesStandardCheckValue()
    {
        Assert.Equal(0xCBF43926u, Crc32.Compute(Encoding.ASCII.GetBytes("123456789")));
    }

    [Fact]
    public void SeedIsEquivalentToPrependedByte()
    {
        byte[] payload = { 0x31, 0x10, 0x20, 0x30 };
        byte[] withSeed = { 0xA2, 0x31, 0x10, 0x20, 0x30 };
        Assert.Equal(Crc32.Compute(withSeed), Crc32.ComputeWithSeed(0xA2, payload));
    }
}
