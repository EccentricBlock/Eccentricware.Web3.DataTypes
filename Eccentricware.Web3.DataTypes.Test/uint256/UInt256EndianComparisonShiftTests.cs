using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using UInt256 = EccentricWare.Web3.DataTypes.uint256;

namespace EccentricWare.Web3.DataTypes.Tests.uint256;

[TestClass]
public sealed class UInt256EndianComparisonShiftTests
{
    [TestMethod]
    public void WriteBigEndian_One_IsLastByteOne()
    {
        Span<byte> bytes = stackalloc byte[32];
        UInt256.One.WriteBigEndian(bytes);

        for (int i = 0; i < 31; i++)
            Assert.AreEqual((byte)0, bytes[i], $"Byte[{i}] must be zero.");

        Assert.AreEqual((byte)1, bytes[31]);
    }

    [TestMethod]
    public void WriteLittleEndian_One_IsFirstByteOne()
    {
        Span<byte> bytes = stackalloc byte[32];
        UInt256.One.WriteLittleEndian(bytes);

        Assert.AreEqual((byte)1, bytes[0]);
        for (int i = 1; i < 32; i++)
            Assert.AreEqual((byte)0, bytes[i], $"Byte[{i}] must be zero.");
    }

    [TestMethod]
    public void FromBigEndian32_RoundTrips()
    {
        UInt256 original = new UInt256(
            limb0: 0x1111111111111111UL,
            limb1: 0x2222222222222222UL,
            limb2: 0x3333333333333333UL,
            limb3: 0x4444444444444444UL);

        Span<byte> bytes = stackalloc byte[32];
        original.WriteBigEndian(bytes);

        UInt256 parsed = UInt256.FromBigEndian32(bytes);
        Assert.AreEqual(original, parsed);
    }

    [TestMethod]
    public void CompareTo_OrdersByMostSignificantLimb()
    {
        UInt256 low = new UInt256(limb0: ulong.MaxValue, limb1: ulong.MaxValue, limb2: ulong.MaxValue, limb3: 0);
        UInt256 high = new UInt256(limb0: 0, limb1: 0, limb2: 0, limb3: 1);

        Assert.IsTrue(high > low);
        Assert.IsTrue(low < high);
        Assert.AreEqual(0, low.CompareTo(low));
        Assert.AreNotEqual(0, high.CompareTo(low));
    }

    [TestMethod]
    public void Equals_AndHashCode_Consistent()
    {
        UInt256 a = new UInt256(123UL);
        UInt256 b = new UInt256(123UL);
        UInt256 c = new UInt256(124UL);

        Assert.IsTrue(a.Equals(b));
        Assert.IsTrue(a == b);
        Assert.IsFalse(a != b);

        Assert.IsFalse(a.Equals(c));
        Assert.IsTrue(a != c);

        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
    }

    [TestMethod]
    public void GetStableHash64_SameValueSameHash_DifferentValueDifferentHashForBasicCases()
    {
        UInt256 a = new UInt256(1UL);
        UInt256 b = new UInt256(1UL);
        UInt256 c = new UInt256(2UL);
        UInt256 d = new UInt256(limb0: 0, limb1: 1, limb2: 0, limb3: 0);

        Assert.AreEqual(a.GetStableHash64(), b.GetStableHash64());
        Assert.AreNotEqual(a.GetStableHash64(), c.GetStableHash64());
        Assert.AreNotEqual(a.GetStableHash64(), d.GetStableHash64());
    }

    [TestMethod]
    public void ShiftLeftAndRight_WorksAcrossWordBoundaries()
    {
        UInt256 one = UInt256.One;

        Assert.AreEqual(new UInt256(2UL), one << 1);
        Assert.AreEqual(one, (one << 1) >> 1);

        UInt256 shifted64 = one << 64;
        Assert.AreEqual(new UInt256(limb0: 0, limb1: 1, limb2: 0, limb3: 0), shifted64);
        Assert.AreEqual(one, shifted64 >> 64);

        UInt256 shifted128 = one << 128;
        Assert.AreEqual(new UInt256(limb0: 0, limb1: 0, limb2: 1, limb3: 0), shifted128);
        Assert.AreEqual(one, shifted128 >> 128);

        UInt256 shifted192 = one << 192;
        Assert.AreEqual(new UInt256(limb0: 0, limb1: 0, limb2: 0, limb3: 1), shifted192);
        Assert.AreEqual(one, shifted192 >> 192);

        Assert.AreEqual(UInt256.Zero, one << 256);
        Assert.AreEqual(UInt256.Zero, one >> 256);

        // Shift by 255 still valid
        UInt256 topBit = one << 255;
        Assert.IsFalse(topBit.IsZero);
        Assert.AreEqual(one, topBit >> 255);
    }
}
