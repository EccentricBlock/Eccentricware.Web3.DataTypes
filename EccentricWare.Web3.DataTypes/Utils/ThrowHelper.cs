using System.Diagnostics.CodeAnalysis;

namespace EccentricWare.Web3.DataTypes.Utils;


/// <summary>
/// Helper class to throw exceptions outside hot paths.
/// Keeps throwing code out of inlined methods for better JIT optimization.
/// Consolidated for all data types (Hash32, Address, etc.).
/// </summary>
internal static class ThrowHelper
{
    // Hash32 exceptions
    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidLength(string paramName)
        => throw new ArgumentException($"Hash32 requires exactly {Hash32.ByteLength} bytes", paramName);

    [DoesNotReturn]
    public static void ThrowArgumentExceptionDestinationTooSmall(string paramName)
        => throw new ArgumentException("Destination buffer is too small", paramName);

    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidHexLength()
        => throw new FormatException($"Hash32 requires exactly {Hash32.HexLength} hex characters (32 bytes)");

    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidHex()
        => throw new FormatException("Invalid hex character");

    // Address exceptions
    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidEvmLength(string paramName)
        => throw new ArgumentException($"EVM address requires exactly {Address.EvmByteLength} bytes", paramName);

    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidSolanaLength(string paramName)
        => throw new ArgumentException($"Solana address requires exactly {Address.SolanaByteLength} bytes", paramName);

    [DoesNotReturn]
    public static void ThrowFormatExceptionEmpty()
        => throw new FormatException("Address string cannot be empty");

    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidEvmHexLength()
        => throw new FormatException($"EVM address requires exactly {Address.EvmHexLength} hex characters (20 bytes)");

    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidBase58Length()
        => throw new FormatException($"Solana address must be 1-{Address.MaxBase58Length} Base58 characters");

    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidBase58()
        => throw new FormatException("Invalid Base58 string for Solana address");

    // FunctionSelector exceptions
    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidFunctionSelectorLength(string paramName)
        => throw new ArgumentException($"FunctionSelector requires exactly {FunctionSelector.ByteLength} bytes", paramName);

    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidFunctionSelectorHexLength()
        => throw new FormatException($"FunctionSelector requires exactly {FunctionSelector.HexLength} hex characters (4 bytes)");

    // Signature exceptions
    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidSignatureRLength(string paramName)
        => throw new ArgumentException("Signature r component requires exactly 32 bytes", paramName);

    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidSignatureSLength(string paramName)
        => throw new ArgumentException("Signature s component requires exactly 32 bytes", paramName);

    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidEvmSignatureLength(string paramName)
        => throw new ArgumentException($"EVM signature requires exactly {Signature.EvmByteLength} bytes", paramName);

    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidSolanaSignatureLength(string paramName)
        => throw new ArgumentException($"Solana signature requires exactly {Signature.SolanaByteLength} bytes", paramName);

    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidSignatureHexLength()
        => throw new FormatException($"Signature requires {Signature.EvmHexLength} (EVM) or {Signature.SolanaHexLength} (Solana) hex characters");

    // HexBytes exceptions
    [DoesNotReturn]
    public static void ThrowFormatExceptionOddHexLength()
        => throw new FormatException("Hex string must have an even number of characters");
}
