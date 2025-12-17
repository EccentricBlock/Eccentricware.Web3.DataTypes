using System.Runtime.CompilerServices;

namespace EccentricWare.Web3.DataTypes.Utils;

public static class ByteUtils
{

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParseHexNibbleUtf8(byte b)
    {
        int v = b;
        int d = v - '0';
        int l = (v | 0x20) - 'a' + 10;

        if ((uint)d <= 9) return d;
        if ((uint)(l - 10) <= 5) return l;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int ParseHexNibble(char c)
    {
        // Branchless hex nibble parsing
        int val = c;
        int digit = val - '0';
        int lower = (val | 0x20) - 'a' + 10; // Case-insensitive a-f

        if ((uint)digit <= 9) return digit;
        if ((uint)(lower - 10) <= 5) return lower;
        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryHexNibble(byte c, out byte value)
    {
        if ((uint)(c - '0') <= 9)
        {
            value = (byte)(c - '0');
            return true;
        }

        c |= 0x20;
        if ((uint)(c - 'a') <= 5)
        {
            value = (byte)(c - 'a' + 10);
            return true;
        }

        value = 0;
        return false;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ulong ParseHexUInt64(ReadOnlySpan<char> hex)
    {
        ulong result = 0;
        for (int i = 0; i < 16; i++)
        {
            int nibble = ParseHexNibble(hex[i]);
            if (nibble < 0) ThrowHelper.ThrowFormatExceptionInvalidHex();
            result = (result << 4) | (uint)nibble;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64(ReadOnlySpan<char> hex, out ulong result)
    {
        result = 0;
        for (int i = 0; i < 16; i++)
        {
            int nibble = ParseHexNibble(hex[i]);
            if (nibble < 0) return false;
            result = (result << 4) | (uint)nibble;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint ParseHexUInt32Utf8(ReadOnlySpan<byte> hex)
    {
        uint result = 0;
        for (int i = 0; i < hex.Length; i++)
        {
            int nibble = ParseHexNibbleUtf8(hex[i]);
            if (nibble < 0)
                ThrowHelper.ThrowFormatExceptionInvalidHex();
            result = (result << 4) | (uint)nibble;
        }
        return result;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt32Utf8(ReadOnlySpan<byte> hex, out uint result)
    {
        result = 0;
        for (int i = 0; i < hex.Length; i++)
        {
            int nibble = ParseHexNibbleUtf8(hex[i]);
            if (nibble < 0)
                return false;
            result = (result << 4) | (uint)nibble;
        }
        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseHexUInt64Utf8(ReadOnlySpan<byte> hex, out ulong value)
    {
        value = 0;

        // Unrolled fixed-width loop (exactly 16 nibbles)
        for (int i = 0; i < 16; i++)
        {
            int nibble = ByteUtils.ParseHexNibbleUtf8(hex[i]);
            if (nibble < 0)
                return false;

            value = (value << 4) | (uint)nibble;
        }

        return true;
    }
}
