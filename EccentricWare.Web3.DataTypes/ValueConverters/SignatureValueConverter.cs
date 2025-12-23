using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;


/// <summary>
/// EF Core value converter for <see cref="Signature"/> using a compact fixed-length binary encoding.
/// </summary>
/// <remarks>
/// Provider layout (66 bytes):
/// - [0]      : SignatureType (1 byte)
/// - [1..64]  : Signature payload (64 bytes)
/// - [65]     : EVM v byte (0 for Solana)
///
/// Recommended column types:
/// - SQL Server: BINARY(66) or VARBINARY(66)
/// - PostgreSQL: BYTEA (enforce length via constraint if needed)
/// </remarks>
/// <remarks>
/// Creates the converter with an optional mapping hint for fixed-size storage.
/// </remarks>
public sealed class SignatureValueConverter(ConverterMappingHints? mappingHints = null) : ValueConverter<Signature, byte[]>(ToProviderExpression, FromProviderExpression,
        mappingHints ?? new ConverterMappingHints(size: 66))
{
    private static readonly Expression<Func<Signature, byte[]>> ToProviderExpression
        = sig => ToProviderBytes(sig);

    private static readonly Expression<Func<byte[], Signature>> FromProviderExpression
        = bytes => FromProviderBytes(bytes);

    /// <summary>
    /// Converts a <see cref="Signature"/> to the 66-byte provider encoding.
    /// </summary>
    private static byte[] ToProviderBytes(Signature signature)
    {
        byte[] bytes = new byte[66];
        bytes[0] = (byte)signature.Type;

        // Writes 64 payload bytes; for EVM writes v into offset 64 of this span (i.e. bytes[65]).
        signature.WriteBytes(bytes.AsSpan(1));
        return bytes;
    }

    /// <summary>
    /// Converts the 66-byte provider encoding to a <see cref="Signature"/>.
    /// </summary>
    private static Signature FromProviderBytes(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);

        if (bytes.Length != 66)
            throw new ArgumentException("Invalid signature byte length; expected 66 bytes.", nameof(bytes));

        SignatureType type = (SignatureType)bytes[0];

        return type switch
        {
            SignatureType.Evm => Signature.FromEvmBytes(bytes.AsSpan(1, Signature.EvmByteLength)),
            SignatureType.Solana => Signature.FromSolanaBytes(bytes.AsSpan(1, Signature.SolanaByteLength)),
            _ => throw new ArgumentOutOfRangeException(nameof(bytes), "Unknown signature type byte.")
        };
    }
}