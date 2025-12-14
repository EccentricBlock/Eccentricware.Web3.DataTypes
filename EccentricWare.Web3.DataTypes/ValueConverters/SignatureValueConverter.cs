using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core ValueConverter for Signature storing as fixed 66-byte binary.
/// 
/// Storage format: [1 byte type][65 bytes max data]
/// - EVM signatures: type (0) + 65 bytes (r + s + v)
/// - Solana signatures: type (1) + 64 bytes + 1 byte padding
/// 
/// Fixed-size storage optimized for indexed lookups:
/// - Consistent B-tree page packing
/// - Fast memcmp comparisons
/// - Predictable row sizes
/// </summary>
public sealed class SignatureValueConverter : ValueConverter<Signature, byte[]>
{
    /// <summary>
    /// Fixed storage size: 1 byte type + 65 bytes data (EVM max).
    /// Solana signatures use 64 bytes + 1 byte padding.
    /// </summary>
    public const int StorageSize = 1 + Signature.EvmByteLength; // 66

    private static readonly Expression<Func<Signature, byte[]>> ToProviderExpr = 
        static v => ToBytes(v);
    
    private static readonly Expression<Func<byte[], Signature>> FromProviderExpr = 
        static v => FromBytes(v);

    public SignatureValueConverter() : base(ToProviderExpr, FromProviderExpr)
    {
    }

    public SignatureValueConverter(ConverterMappingHints? mappingHints) 
        : base(ToProviderExpr, FromProviderExpr, mappingHints)
    {
    }

    /// <summary>
    /// Default mapping hints for database column configuration.
    /// Specifies fixed 66-byte binary storage.
    /// </summary>
    public static readonly ConverterMappingHints DefaultHints = new(size: StorageSize);

    private static byte[] ToBytes(Signature value)
    {
        // Fixed 66-byte array: [type][65 bytes data]
        var bytes = new byte[StorageSize];
        
        // First byte is the signature type
        bytes[0] = (byte)value.Type;
        
        // Write signature data (64 or 65 bytes)
        // For Solana, last byte stays as zero (padding)
        value.WriteBytes(bytes.AsSpan(1, value.ByteLength));
        
        return bytes;
    }

    private static Signature FromBytes(byte[] bytes)
    {
        var type = (SignatureType)bytes[0];
        
        return type switch
        {
            // EVM: read 65 bytes
            SignatureType.Evm => Signature.FromEvmBytes(bytes.AsSpan(1, Signature.EvmByteLength)),
            // Solana: read 64 bytes
            SignatureType.Solana => Signature.FromSolanaBytes(bytes.AsSpan(1, Signature.SolanaByteLength)),
            _ => throw new InvalidOperationException($"Unknown signature type: {type}")
        };
    }
}

