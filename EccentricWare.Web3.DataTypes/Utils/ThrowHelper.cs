using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using System.Text.Json;

namespace EccentricWare.Web3.DataTypes.Utils;


/// <summary>
/// Helper class to throw exceptions outside hot paths.
/// Keeps throwing code out of inlined methods for better JIT optimization.
/// Consolidated for all data types (Hash32, Address, etc.).
/// </summary>
internal static class ThrowHelper
{
    /// <summary>
    /// Throws an <see cref="ArgumentNullException"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentNullException(string paramName)
        => throw new ArgumentNullException(paramName);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for an invalid function selector length.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentExceptionInvalidFunctionSelectorLength(string paramName)
        => throw new ArgumentException("Function selector must be at least 4 bytes.", paramName);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> for an invalid discriminator length.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentExceptionInvalidDiscriminatorLength(string paramName)
        => throw new ArgumentException("Instruction discriminator must be at least 8 bytes.", paramName);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when a destination span is too small.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentExceptionDestinationTooSmall(string paramName)
        => throw new ArgumentException("Destination buffer is too small.", paramName);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when an argument is the wrong runtime type.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentExceptionWrongType(string paramName, string expectedTypeName)
        => throw new ArgumentException($"Object must be of type {expectedTypeName}.", paramName);

    /// <summary>
    /// Throws an <see cref="ArgumentException"/> when a fixed-size span is the wrong length.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowArgumentExceptionFixedSize(string paramName, int requiredLength)
        => throw new ArgumentException($"Destination must be exactly {requiredLength} elements long.", paramName);

    /// <summary>
    /// Throws a <see cref="FormatException"/> for an invalid function selector.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowFormatExceptionInvalidFunctionSelector()
        => throw new FormatException("Invalid function selector hex value.");

    /// <summary>
    /// Throws a <see cref="FormatException"/> for an invalid instruction discriminator.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowFormatExceptionInvalidDiscriminator()
        => throw new FormatException("Invalid instruction discriminator hex value.");

    /// <summary>
    /// Throws a <see cref="FormatException"/> for an unsupported format string.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowFormatExceptionUnknownFormat(ReadOnlySpan<char> format)
        => throw new FormatException($"Unknown format: \"{format.ToString()}\"");

    /// <summary>
    /// Throws a <see cref="JsonException"/> indicating a string token was expected.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowJsonExceptionExpectedString(string typeName)
        => throw new JsonException($"Expected JSON string for {typeName}.");

    /// <summary>
    /// Throws a <see cref="JsonException"/> indicating an invalid value for the target type.
    /// </summary>
    [MethodImpl(MethodImplOptions.NoInlining)]
    public static void ThrowJsonExceptionInvalidValue(string typeName)
        => throw new JsonException($"Invalid value for {typeName}.");
    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidBase64()
     => throw new FormatException("Invalid Base64 encoded value.");

    [DoesNotReturn]
    public static void ThrowFormatExceptionInvalidAddress()
        => throw new FormatException("Invalid blockchain address format");


    // Hash32 exceptions
    [DoesNotReturn]
    public static void ThrowArgumentExceptionInvalidLength(string paramName)
        => throw new ArgumentException($"Hash32 requires exactly {Hash32.ByteLength} bytes", paramName);


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
