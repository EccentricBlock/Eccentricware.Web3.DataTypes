using EccentricWare.Web3.DataTypes.Utils;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_CoreTests
{
    [TestMethod]
    [DataRow(0, 1UL)]
    [DataRow(1, 10UL)]
    [DataRow(2, 100UL)]
    [DataRow(3, 1000UL)]
    [DataRow(19, 10_000_000_000_000_000_000UL)]
    public void Pow10U64_ReturnsExpected(int exponent, ulong expected)
    {
        Assert.AreEqual(expected, ByteUtils.Pow10U64(exponent));
    }

    [TestMethod]
    [DataRow(0UL, 0)]
    [DataRow(1UL, 1)]
    [DataRow(0xFFUL, 1)]
    [DataRow(0x100UL, 2)]
    [DataRow(0xFFFFUL, 2)]
    [DataRow(0x1_0000UL, 3)]
    [DataRow(ulong.MaxValue, 8)]
    public void GetByteCount_ReturnsExpected(ulong value, int expected)
    {
        Assert.AreEqual(expected, ByteUtils.GetByteCount(value));
    }

    [TestMethod]
    public void HexAlphabets_AreCorrect()
    {
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("0123456789abcdef"), ByteUtils.HexBytesLower.ToArray());
        CollectionAssert.AreEqual(Encoding.ASCII.GetBytes("0123456789ABCDEF"), ByteUtils.HexBytesUpper.ToArray());
    }
}