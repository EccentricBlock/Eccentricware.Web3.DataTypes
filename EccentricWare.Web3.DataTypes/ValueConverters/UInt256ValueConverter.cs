using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core ValueConverter for uint256 storing as fixed 32-byte big-endian binary.
/// Optimal for database indexing: fixed-size binary enables efficient B-tree indexes.
/// Zero intermediate allocations during conversion.
/// </summary>
public sealed class UInt256ValueConverter : ValueConverter<uint256, byte[]>
{
    /// <summary>
    /// The fixed storage size in bytes.
    /// </summary>
    public const int StorageSize = 32;

    private static readonly Expression<Func<uint256, byte[]>> ToProviderExpr = 
        static v => ToBytes(v);
    
    private static readonly Expression<Func<byte[], uint256>> FromProviderExpr = 
        static v => FromBytes(v);

    public UInt256ValueConverter() : base(ToProviderExpr, FromProviderExpr)
    {
    }

    public UInt256ValueConverter(ConverterMappingHints? mappingHints) 
        : base(ToProviderExpr, FromProviderExpr, mappingHints)
    {
    }

    /// <summary>
    /// Default mapping hints for database column configuration.
    /// Specifies fixed 32-byte binary storage.
    /// </summary>
    public static readonly ConverterMappingHints DefaultHints = new(size: StorageSize);

    private static byte[] ToBytes(uint256 value)
    {
        var bytes = new byte[StorageSize];
        value.WriteBigEndian(bytes);
        return bytes;
    }

    private static uint256 FromBytes(byte[] bytes) => new(bytes);
}

