using System.Linq.Expressions;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core ValueConverter for HexBigInteger storing as compact variable-size binary.
/// 
/// Storage format:
/// - Positive values: Big-endian unsigned bytes (minimal representation)
/// - Negative values: First byte 0xFF marker, followed by two's complement big-endian bytes
/// - Zero: Empty array (0 bytes)
/// 
/// This format enables efficient storage and preserves sign information.
/// For indexing large integers, consider using uint256 if values fit within 256 bits.
/// </summary>
public sealed class HexBigIntegerValueConverter : ValueConverter<HexBigInteger, byte[]>
{
    /// <summary>
    /// Marker byte indicating a negative value follows.
    /// </summary>
    private const byte NegativeMarker = 0xFF;

    private static readonly Expression<Func<HexBigInteger, byte[]>> ToProviderExpr = 
        static v => ToBytes(v);
    
    private static readonly Expression<Func<byte[], HexBigInteger>> FromProviderExpr = 
        static v => FromBytes(v);

    public HexBigIntegerValueConverter() : base(ToProviderExpr, FromProviderExpr)
    {
    }

    public HexBigIntegerValueConverter(ConverterMappingHints? mappingHints) 
        : base(ToProviderExpr, FromProviderExpr, mappingHints)
    {
    }

    private static byte[] ToBytes(HexBigInteger value)
    {
        var bigInt = value.Value;
        
        if (bigInt.IsZero)
            return [];

        if (bigInt.Sign > 0)
        {
            // Positive: store as unsigned big-endian bytes
            return bigInt.ToByteArray(isUnsigned: true, isBigEndian: true);
        }
        else
        {
            // Negative: marker byte + big-endian two's complement
            var bytes = bigInt.ToByteArray(isUnsigned: false, isBigEndian: true);
            
            // If first byte could be mistaken for marker, we need to check
            // But since we use marker + bytes, we just prepend the marker
            var result = new byte[1 + bytes.Length];
            result[0] = NegativeMarker;
            bytes.CopyTo(result.AsSpan(1));
            return result;
        }
    }

    private static HexBigInteger FromBytes(byte[] bytes)
    {
        if (bytes.Length == 0)
            return HexBigInteger.Zero;

        if (bytes[0] == NegativeMarker && bytes.Length > 1)
        {
            // Negative value: skip marker, read as signed
            var bigInt = new BigInteger(bytes.AsSpan(1), isUnsigned: false, isBigEndian: true);
            return new HexBigInteger(bigInt);
        }
        else
        {
            // Positive value: read as unsigned
            var bigInt = new BigInteger(bytes, isUnsigned: true, isBigEndian: true);
            return new HexBigInteger(bigInt);
        }
    }
}

