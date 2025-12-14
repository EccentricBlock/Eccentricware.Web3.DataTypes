using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core ValueConverter for int256 storing as fixed 32-byte big-endian binary.
/// Two's complement representation preserves sign for database storage.
/// 
/// Optimal for database indexing: fixed-size binary enables efficient B-tree indexes.
/// Note: Signed comparison in database may differ from int256 comparison
/// unless using signed binary types or application-level sorting.
/// </summary>
public sealed class Int256ValueConverter : ValueConverter<int256, byte[]>
{
    /// <summary>
    /// The fixed storage size in bytes.
    /// </summary>
    public const int StorageSize = 32;

    private static readonly Expression<Func<int256, byte[]>> ToProviderExpr = 
        static v => ToBytes(v);
    
    private static readonly Expression<Func<byte[], int256>> FromProviderExpr = 
        static v => FromBytes(v);

    public Int256ValueConverter() : base(ToProviderExpr, FromProviderExpr)
    {
    }

    public Int256ValueConverter(ConverterMappingHints? mappingHints) 
        : base(ToProviderExpr, FromProviderExpr, mappingHints)
    {
    }

    /// <summary>
    /// Default mapping hints for database column configuration.
    /// Specifies fixed 32-byte binary storage.
    /// </summary>
    public static readonly ConverterMappingHints DefaultHints = new(size: StorageSize);

    private static byte[] ToBytes(int256 value)
    {
        var bytes = new byte[StorageSize];
        value.WriteBigEndian(bytes);
        return bytes;
    }

    private static int256 FromBytes(byte[] bytes) => new(bytes);
}

