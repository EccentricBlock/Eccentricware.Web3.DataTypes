using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core ValueConverter for Hash32 storing as fixed 32-byte big-endian binary.
/// Optimal for database indexing: fixed-size binary enables efficient B-tree indexes
/// and maintains lexicographic ordering for range queries.
/// Zero intermediate allocations during conversion.
/// </summary>
public sealed class Hash32ValueConverter : ValueConverter<Hash32, byte[]>
{
    /// <summary>
    /// The fixed storage size in bytes.
    /// </summary>
    public const int StorageSize = Hash32.ByteLength; // 32

    private static readonly Expression<Func<Hash32, byte[]>> ToProviderExpr = 
        static v => ToBytes(v);
    
    private static readonly Expression<Func<byte[], Hash32>> FromProviderExpr = 
        static v => FromBytes(v);

    public Hash32ValueConverter() : base(ToProviderExpr, FromProviderExpr)
    {
    }

    public Hash32ValueConverter(ConverterMappingHints? mappingHints) 
        : base(ToProviderExpr, FromProviderExpr, mappingHints)
    {
    }

    /// <summary>
    /// Default mapping hints for database column configuration.
    /// Specifies fixed 32-byte binary storage.
    /// </summary>
    public static readonly ConverterMappingHints DefaultHints = new(size: StorageSize);

    private static byte[] ToBytes(Hash32 value)
    {
        var bytes = new byte[StorageSize];
        value.WriteBigEndian(bytes);
        return bytes;
    }

    private static Hash32 FromBytes(byte[] bytes) => new(bytes);
}

