using EccentricWare.Web3.DataTypes.Utils;
using System.Text;

namespace Eccentricware.Web3.DataTypes.Test.ByteUtilsTests;

[TestClass]
public sealed class ByteUtils_Base64Tests
{
    [TestMethod]
    public void TryDecodeBase64Utf8_Standard_Works()
    {
        byte[] payload = Encoding.UTF8.GetBytes("hello world");
        string b64 = Convert.ToBase64String(payload);

        Span<byte> dst = stackalloc byte[64];
        Assert.IsTrue(ByteUtils.TryDecodeBase64Utf8(Encoding.ASCII.GetBytes(b64), dst, out int written));
        CollectionAssert.AreEqual(payload, dst.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryDecodeBase64Utf8_UrlSafe_NoPadding_Works()
    {
        byte[] payload = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        string b64 = Convert.ToBase64String(payload);

        // Convert to URL-safe and strip padding
        string url = b64.Replace('+', '-').Replace('/', '_').TrimEnd('=');

        Span<byte> dst = stackalloc byte[64];
        Assert.IsTrue(ByteUtils.TryDecodeBase64Utf8(Encoding.ASCII.GetBytes(url), dst, out int written));
        CollectionAssert.AreEqual(payload, dst.Slice(0, written).ToArray());
    }

    [TestMethod]
    public void TryDecodeBase64Utf8_InvalidOrTooSmall_ReturnsFalse()
    {
        Span<byte> dstSmall = stackalloc byte[1];

        Assert.IsFalse(ByteUtils.TryDecodeBase64Utf8(ReadOnlySpan<byte>.Empty, dstSmall, out _));
        Assert.IsFalse(ByteUtils.TryDecodeBase64Utf8("###"u8, dstSmall, out _));

        // Valid base64 but destination too small
        byte[] payload = Encoding.UTF8.GetBytes("this is longer than 1 byte");
        string b64 = Convert.ToBase64String(payload);
        Assert.IsFalse(ByteUtils.TryDecodeBase64Utf8(Encoding.ASCII.GetBytes(b64), dstSmall, out _));
    }

    [TestMethod]
    public void TryDecodeBase64Utf8_SizeGuard_RejectsOver256Normalised()
    {
        // 257 bytes of base64 text will normalise to >= 257, should reject (guard is >256).
        // Use urlsafe text to force normalisation path.
        byte[] big = Enumerable.Repeat((byte)'A', 257).ToArray();
        Span<byte> dst = stackalloc byte[512];
        Assert.IsFalse(ByteUtils.TryDecodeBase64Utf8(big, dst, out _));
    }
}