using EccentricWare.Web3.DataTypes.Utils;
using Microsoft.VisualStudio.TestTools.UnitTesting;

[TestClass]
public class KeccakTests
{
    [TestMethod]
    public void Keccak256_EmptyString_MatchesKnownVector()
    {
        // Known Keccak-256("") = c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470
        Span<byte> out32 = stackalloc byte[32];
        ReadOnlySpan<byte> input = ReadOnlySpan<byte>.Empty;
        Keccak256.ComputeHash(input, out32);
        string hex = System.BitConverter.ToString(out32.ToArray()).Replace("-", string.Empty).ToLowerInvariant();
        Assert.AreEqual("c5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470", hex);
    }
}
