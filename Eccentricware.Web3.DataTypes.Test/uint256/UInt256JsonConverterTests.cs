using System.Text.Json;
using EccentricWare.Web3.DataTypes.JsonConverters;
using EccentricWare.Web3.DataTypes.Utils.Uint256;
using UInt256 = EccentricWare.Web3.DataTypes.uint256;

namespace EccentricWare.Web3.DataTypes.Tests;

[TestClass]
public sealed class UInt256JsonConverterTests
{
    [TestMethod]
    public void JsonConverter_Deserialize_EvmHexString_Works()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UInt256JsonConverter(
            parseMode: UInt256ParseMode.EvmQuantityStrict,
            writeFormat: "0x"));

        UInt256 v = JsonSerializer.Deserialize<UInt256>("\"0x1\"", options);
        Assert.AreEqual(UInt256.One, v);

        UInt256 z = JsonSerializer.Deserialize<UInt256>("\"0x0\"", options);
        Assert.AreEqual(UInt256.Zero, z);

        UInt256 big = JsonSerializer.Deserialize<UInt256>("\"0x10000000000000000\"", options); // 2^64
        Assert.AreEqual(new UInt256(limb0: 0, limb1: 1, limb2: 0, limb3: 0), big);
    }


    [TestMethod]
    public void JsonConverter_Serialize_Default_WritesJsonString()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UInt256JsonConverter(
            parseMode: UInt256ParseMode.EvmQuantityStrict,
            writeFormat: "0x"));

        string jsonOne = JsonSerializer.Serialize(UInt256.One, options);
        Assert.AreEqual("\"0x1\"", jsonOne);

        string jsonZero = JsonSerializer.Serialize(UInt256.Zero, options);
        Assert.AreEqual("\"0x0\"", jsonZero);
    }

    [TestMethod]
    public void JsonConverter_Serialize_FixedWidth64_WritesExpectedLength()
    {
        var options = new JsonSerializerOptions();
        options.Converters.Add(new UInt256JsonConverter(
            parseMode: UInt256ParseMode.EvmQuantityStrict,
            writeFormat: "0x64"));

        string json = JsonSerializer.Serialize(UInt256.One, options);

        // JSON string includes quotes: "0x" + 64 digits => 66 chars inside quotes => 68 with quotes.
        Assert.AreEqual(68, json.Length);
        Assert.IsTrue(json.StartsWith("\"0x", StringComparison.Ordinal));
        Assert.IsTrue(json.EndsWith("1\"", StringComparison.Ordinal));
    }
}
