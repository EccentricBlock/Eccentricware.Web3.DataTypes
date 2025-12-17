# EccentricWare.Web3.DataTypes

A compact, allocation-minimal set of immutable value types for representing blockchain primitives in .NET 10.  

Designed for **very low CPU and memory overhead**, with each type fully independent and optimised for JSON-RPC responses, calldata, and event data.

---

## Overview

This library provides **native C# value types** for common EVM and Solana primitives in a unified structure, focusing on:

- Zero/low allocation parsing and formatting
- Predictable memory layouts
- Span-based APIs (`ReadOnlySpan<char>`, `ReadOnlySpan<byte>`)
- Explicit opt-in zero-copy paths
- `JsonConverter` implentations for all types
- EF Core `ValueConverter` implentations for all types

### Included types
- `HexBytes`
- `Hash32`
- `Address`
- `FunctionSelector`
- `Signature`
- `HexBigInteger`
- `uint256`, `int256`

All types are immutable, thread-safe, and optimised for high-throughput pipelines.

---

## Install

```powershell
dotnet add package EccentricWare.Web3.DataTypes
````

---

## Usage

### Parsing and formatting (zero-allocation friendly)

```csharp
var hb = HexBytes.Parse("0xdeadbeef");

ReadOnlySpan<byte> utf8 = "0xdeadbeef"u8;
HexBytes.TryParse(utf8, out var parsed);

Span<char> buf = stackalloc char[hb.HexLength + 2];
hb.TryFormat(buf, out _);
```

### Zero-copy (explicit and unsafe)

```csharp
byte[] bytes = { 0xDE, 0xAD, 0xBE, 0xEF };

// Defensive copy
var safe = new HexBytes(bytes);

// Zero-copy (caller guarantees immutability)
var unsafeZeroCopy = HexBytes.FromArrayUnsafe(bytes);
```

### EVM helpers

```csharp
var selector = hb.GetFunctionSelector();
var calldata = hb.GetCalldataParams();
var padded = hb.PadToWord(); // 32-byte EVM word
```

---

## EF Core ValueConverters

Optimised converters for efficient storage and indexing, with fixed-size binary layouts where possible.

Available converters include:

* `Hash32` → `binary(32)`
* `Address` → `binary(33)` (type byte + data)
* `FunctionSelector` → `int` or `binary(4)`
* `uint256` / `int256` → `binary(32)`
* `Signature` → `binary(66)`
* `HexBytes`, `HexBigInteger`

Example:

```csharp
eb.Property(t => t.FunctionSelector)
  .HasConversion<FunctionSelectorValueConverter>();

eb.Property(t => t.Amount)
  .HasConversion(new UInt256ValueConverter(), UInt256ValueConverter.DefaultHints);
```

---

## JSON Converters

All core types include `System.Text.Json` converters with UTF-8, span-based implementations.

Features:

* Accepts `0x` hex and (where applicable) decimal
* Avoids intermediate string allocations
* Supports throwing (`Parse`) and non-throwing (`TryParse`) paths

Register globally:

```csharp
var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
options.Converters.Add(new AddressJsonConverter());
options.Converters.Add(new UInt256JsonConverter());
```

---

## Performance & Safety Notes

* Constructors copy by default to preserve immutability
* `FromArrayUnsafe` performs **no copy** — use only when lifetime and immutability are guaranteed
* `TryParse` APIs minimise allocations and work directly on UTF-8 spans
* `GetHashCode()` is lightweight and non-cryptographic

---

## Design Principles

* Minimal allocations and predictable CPU cost
* Fixed-size types for hot paths and indexing
* Explicit APIs where safety vs performance is a caller decision

---

## Compatibility

* **Runtime:** .NET 10
* **Language:** C# 14.0

---

## Contributing & Support

Contributions welcome. 

Issues and requests:
[https://github.com/EccentricBlock/Eccentricware.Web3.DataTypes/issues](https://github.com/EccentricBlock/Eccentricware.Web3.DataTypes/issues)
