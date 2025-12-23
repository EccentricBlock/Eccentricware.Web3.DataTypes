using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;


/// <summary>
/// EF Core provider converter for storing <see cref="int256"/> as a fixed-length 32-byte array.
/// Big-endian two's complement encoding is used for stable persistence.
/// Note: lexicographic order of two's complement bytes does not match signed numeric order across the sign boundary.
/// </summary>
public sealed class Int256BytesValueConverter : ValueConverter<int256, byte[]>
{
    /// <summary>
    /// Creates a converter that maps int256 &lt;-&gt; 32-byte big-endian two's complement array.
    /// </summary>
    public Int256BytesValueConverter()
        : base(
            model => ConvertToProvider(model),
            provider => ConvertFromProvider(provider))
    {
    }

    /// <summary>
    /// Converts an int256 to a 32-byte big-endian two's complement array.
    /// </summary>
    private new static byte[] ConvertToProvider(int256 value)
    {
        return value.ToBigEndianBytes();
    }

    /// <summary>
    /// Converts a 32-byte big-endian two's complement array to int256.
    /// </summary>
    private new static int256 ConvertFromProvider(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length != 32)
            throw new FormatException("int256 provider value must be exactly 32 bytes.");

        return new int256(bytes);
    }
}