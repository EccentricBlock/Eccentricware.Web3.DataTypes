using System.Linq.Expressions;
using System.Numerics;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// Converts <see cref="HexBigInteger"/> to and from a compact, signed, big-endian two's-complement <see cref="byte"/> array
/// for database persistence (e.g., VARBINARY/BLOB).
/// </summary>
/// <remarks>
/// This converter is optimised for round-trip correctness and storage efficiency.
/// It does not guarantee lexicographic sort order matches numeric sort order for variable-length byte arrays.
/// </remarks>
/// <remarks>
/// Creates a converter that stores <see cref="HexBigInteger"/> as a signed big-endian two's-complement byte array.
/// </remarks>
/// <param name="mappingHints">Optional EF mapping hints (e.g., fixed size).</param>
public sealed class HexBigIntegerToBytesConverter(ConverterMappingHints? mappingHints = null) : ValueConverter<HexBigInteger, byte[]>(ToProviderExpression, FromProviderExpression, mappingHints)
{
    /// <summary>
    /// Default singleton instance for reuse in model configuration.
    /// </summary>
    public static readonly HexBigIntegerToBytesConverter Default = new();

    private static readonly Expression<Func<HexBigInteger, byte[]>> ToProviderExpression =
        modelValue =>
            modelValue.Value.IsZero
                ? Array.Empty<byte>()
                : modelValue.Value.ToByteArray(isUnsigned: false, isBigEndian: true);

    private static readonly Expression<Func<byte[], HexBigInteger>> FromProviderExpression =
        providerValue =>
            providerValue == null || providerValue.Length == 0
                ? default
                : new HexBigInteger(new BigInteger(providerValue, isUnsigned: false, isBigEndian: true));
}
