using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace EccentricWare.Web3.DataTypes.ValueConverters;

/// <summary>
/// EF Core ValueConverter for Address storing as fixed 33-byte binary.
/// 
/// Storage format: [1 byte type][32 bytes data (right-padded for EVM)]
/// - EVM addresses: type + 20 bytes data + 12 bytes zero padding
/// - Solana addresses: type + 32 bytes data
/// 
/// Fixed-size storage optimized for high-volume indexed lookups:
/// - Optimal B-tree page packing with predictable row sizes
/// - Fast memcmp comparisons without length prefix parsing
/// - Better CPU cache utilization with aligned, fixed-size data
/// - Consistent index seeks with direct offset calculation
/// 
/// For tens of millions of rows with frequent searches, the query performance
/// benefits of fixed-size columns outweigh the 12-byte storage overhead per EVM address.
/// </summary>
public sealed class AddressValueConverter : ValueConverter<Address, byte[]>
{
    /// <summary>
    /// Fixed storage size: 1 byte type + 32 bytes data.
    /// EVM addresses are zero-padded to maintain fixed size for optimal indexing.
    /// </summary>
    public const int StorageSize = 1 + Address.SolanaByteLength; // 33

    private static readonly Expression<Func<Address, byte[]>> ToProviderExpr = 
        static v => ToBytes(v);
    
    private static readonly Expression<Func<byte[], Address>> FromProviderExpr = 
        static v => FromBytes(v);

    public AddressValueConverter() : base(ToProviderExpr, FromProviderExpr)
    {
    }

    public AddressValueConverter(ConverterMappingHints? mappingHints) 
        : base(ToProviderExpr, FromProviderExpr, mappingHints)
    {
    }

    /// <summary>
    /// Default mapping hints for database column configuration.
    /// Specifies fixed 33-byte binary storage for optimal indexing.
    /// </summary>
    public static readonly ConverterMappingHints DefaultHints = new(size: StorageSize);

    private static byte[] ToBytes(Address value)
    {
        // Fixed 33-byte array: [type][32 bytes data]
        var bytes = new byte[StorageSize];
        
        // First byte is the address type
        bytes[0] = (byte)value.Type;
        
        // Write address data (20 or 32 bytes)
        // For EVM, remaining 12 bytes stay as zeros (padding)
        value.WriteBytes(bytes.AsSpan(1, value.ByteLength));
        
        return bytes;
    }

    private static Address FromBytes(byte[] bytes)
    {
        var type = (AddressType)bytes[0];
        
        return type switch
        {
            // EVM: read only first 20 bytes of data section
            AddressType.Evm => Address.FromEvmBytes(bytes.AsSpan(1, Address.EvmByteLength)),
            // Solana: read all 32 bytes
            AddressType.Solana => Address.FromSolanaBytes(bytes.AsSpan(1, Address.SolanaByteLength)),
            _ => throw new InvalidOperationException($"Unknown address type: {type}")
        };
    }
}

