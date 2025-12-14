using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core ValueConverter for HexBytes storing as variable-length binary (VARBINARY).
/// 
/// This is a direct pass-through converter as HexBytes wraps byte arrays.
/// Database storage is optimized for variable-length binary data.
/// 
/// For indexed columns, consider:
/// - Using a hash column for equality searches
/// - Limiting indexed prefix length for LIKE queries
/// - Using specialized columns for known fixed-size data (e.g., FunctionSelector)
/// </summary>
public sealed class HexBytesValueConverter : ValueConverter<HexBytes, byte[]>
{
    private static readonly Expression<Func<HexBytes, byte[]>> ToProviderExpr = 
        static v => ToBytes(v);
    
    private static readonly Expression<Func<byte[], HexBytes>> FromProviderExpr = 
        static v => FromBytes(v);

    public HexBytesValueConverter() : base(ToProviderExpr, FromProviderExpr)
    {
    }

    public HexBytesValueConverter(ConverterMappingHints? mappingHints) 
        : base(ToProviderExpr, FromProviderExpr, mappingHints)
    {
    }

    private static byte[] ToBytes(HexBytes value)
    {
        // Return a copy to ensure immutability
        return value.ToArray();
    }

    private static HexBytes FromBytes(byte[] bytes)
    {
        // Use unsafe to avoid copy since EF owns this array
        return HexBytes.FromArrayUnsafe(bytes);
    }
}

/// <summary>
/// EF Core ValueConverter for HexBytes storing as hex string.
/// Use when database TEXT storage is preferred over BINARY.
/// 
/// Trade-offs vs binary storage:
/// - 2x storage size (hex encoding)
/// - Human-readable in database tools
/// - Slower conversion (hex encode/decode)
/// - No binary prefix matching
/// </summary>
public sealed class HexBytesStringValueConverter : ValueConverter<HexBytes, string>
{
    private static readonly Expression<Func<HexBytes, string>> ToProviderExpr = 
        static v => v.ToString();
    
    private static readonly Expression<Func<string, HexBytes>> FromProviderExpr = 
        static v => HexBytes.Parse(v);

    public HexBytesStringValueConverter() : base(ToProviderExpr, FromProviderExpr)
    {
    }

    public HexBytesStringValueConverter(ConverterMappingHints? mappingHints) 
        : base(ToProviderExpr, FromProviderExpr, mappingHints)
    {
    }
}

