# opensearch-net: Utf8Json -> System.Text.Json Migration Checklist

## Summary

Bridge layer approach: Replace vendored Utf8Json's JsonWriter/JsonReader implementation with
STJ-backed equivalents while preserving method signatures. All 370+ IJsonFormatter<T> call sites
compile without source changes.

**POC Status**: VERIFIED -- OpenSearch.Net + OpenSearch.Client both compile on all TFMs
(netstandard2.0, netstandard2.1, net6.0, net8.0, net10.0). 14/14 equivalence tests pass.

## Architecture

```
[Existing formatters] -- same method calls --> [JsonWriter/JsonReader struct]
                                                      |
                               +---------------------+---------------------+
                               |                                           |
                     #if !USE_STJ_BRIDGE                        #if USE_STJ_BRIDGE
                               |                                           |
                   [Original Utf8Json]                      [Bridge Layer]
                   - byte[] + offset                        - ArrayBufferWriter<byte>
                   - UnsafeMemory                           - STJ JsonEncodedText (escaping)
                   - IL emit (DynamicObjectResolver)        - Utf8Formatter (numbers)
                   - FarmHash/AutomataDictionary            - STJ Utf8JsonReader (parsing)
                   - DoubleConversion port                  - STJ JsonSerializer (POCO fallback)
```

## Phase 1: Bridge Layer (Current PR -- ready for review)

### Files Added
- [ ] `src/OpenSearch.Net/Serialization/SystemTextJson/Bridge/JsonWriterBridge.cs` (270 lines)
- [ ] `src/OpenSearch.Net/Serialization/SystemTextJson/Bridge/JsonReaderBridge.cs` (405 lines)
- [ ] `src/OpenSearch.Net/Serialization/SystemTextJson/Bridge/DynamicObjectResolverShim.cs` (151 lines)
- [ ] `tests/BridgeEquivalenceTest/` (standalone equivalence test, 14 cases)

### Files Modified (conditional compile guards)
- [ ] `src/OpenSearch.Net/Utf8Json/JsonWriter.cs` -- `#if !USE_STJ_BRIDGE` wrapper
- [ ] `src/OpenSearch.Net/Utf8Json/JsonReader.cs` -- `#if !USE_STJ_BRIDGE` wrapper
- [ ] `src/OpenSearch.Net/Utf8Json/Internal/UnsafeMemory.cs` -- `#if !USE_STJ_BRIDGE`
- [ ] `src/OpenSearch.Net/Utf8Json/Internal/UnsafeMemory.Low.cs` -- `#if !USE_STJ_BRIDGE`
- [ ] `src/OpenSearch.Net/Utf8Json/Resolvers/DynamicObjectResolver.cs` -- `#if !USE_STJ_BRIDGE`
- [ ] `src/OpenSearch.Net/Utf8Json/Formatters/DynamicObjectTypeFallbackFormatter.cs` -- `#if !USE_STJ_BRIDGE`
- [ ] `src/OpenSearch.Net/Utf8Json/Resolvers/StandardResolver.cs` -- 2 conditional points
- [ ] `src/OpenSearch.Net/Serialization/Resolvers/OpenSearchNetFormatterResolver.cs` -- 2 conditional points
- [ ] `src/OpenSearch.Net/OpenSearch.Net.csproj` -- added System.Text.Json + System.Memory for netstandard

### Impact
- **370 IJsonFormatter<T> call sites**: ZERO changes needed
- **168 cross-project references (OpenSearch.Client)**: ZERO changes needed
- **~4300 lines of vendored code excluded** from compilation in bridge mode
- **~800 lines of bridge code** added

### Build verification
```bash
# Original mode (no regression)
dotnet build src/OpenSearch.Client/OpenSearch.Client.csproj

# Bridge mode
dotnet build src/OpenSearch.Client/OpenSearch.Client.csproj -p:DefineConstants=USE_STJ_BRIDGE

# Equivalence test
dotnet run --project tests/BridgeEquivalenceTest/ -p:DefineConstants=USE_STJ_BRIDGE
```

## Phase 2: Enable by Default + Remove Vendored Code

Prerequisites: Phase 1 merged, integration tests pass with bridge mode.

- [ ] Add `USE_STJ_BRIDGE` to default DefineConstants in OpenSearch.Net.csproj
- [ ] Run full test suite with ephemeral cluster (CI)
- [ ] Fix any behavioral differences found in integration tests
- [ ] Performance benchmark: compare throughput/allocation (BenchmarkDotNet)
- [ ] If performance acceptable: delete `#if !USE_STJ_BRIDGE` guarded code blocks
- [ ] Delete `src/OpenSearch.Net/Utf8Json/` directory (all 57 vendored files)
- [ ] Remove `USE_STJ_BRIDGE` conditionals (bridge becomes the only path)
- [ ] Update AGENTS.md to remove "migration incomplete" warning

## Phase 3: Optimize + Modernize

After vendored code removal:

- [ ] Replace `PooledBufferWriter` polyfill with `ArrayBufferWriter<T>` (drop netstandard2.0 or use Microsoft.Bcl.Memory)
- [ ] Add `[JsonSerializable]` source generators for AOT/trimming support
- [ ] Consider dropping netstandard2.0 TFM (net6.0 minimum) to unlock full STJ API surface
- [ ] Replace `DynamicObjectResolverShim` (STJ JsonSerializer fallback) with proper `JsonTypeInfoResolver.Modifiers` for property mapping
- [ ] Remove `IJsonFormatterResolver` abstraction entirely -- use `JsonSerializerOptions` + converter chains
- [ ] Migrate `IJsonFormatter<T>` implementations to `JsonConverter<T>` one module at a time (can be gradual over multiple releases)

## Known Limitations of Bridge Layer

| Limitation | Impact | Fix Timeline |
|---|---|---|
| `DynamicObjectResolverShim` uses STJ for POCOs -- may serialize differently than IL emit version for edge cases (private fields, custom constructors) | Low -- only affects unregistered POCO types that fall through to DynamicObjectResolver | Phase 2 integration tests |
| `Buffer`/`Offset` direct access returns copy (not live reference) | ~20 sites in DynamicObjectResolver (excluded in bridge mode) | N/A -- excluded code |
| `JsonReader` is `ref struct` in bridge mode vs regular `struct` in original | Any code storing JsonReader in a field won't compile | None found in current codebase |
| `WriteRaw` loses Utf8Json's zero-copy UnsafeMemory optimization | Performance -- extra buffer copy for pre-encoded property names | Phase 3 optimization |
| `ReadPropertyNameSegmentRaw` returns allocated byte[] vs zero-alloc slice | Performance -- allocation per property name read | Phase 3 optimization |
| `System.Text.Json` + `System.Memory` added as explicit dependencies for netstandard2.0/2.1 | Package size -- minimal (STJ was already transitive dep) | Acceptable |

## Migration Metrics

| Metric | Before | After (Phase 1) | After (Phase 2) |
|---|---|---|---|
| Vendored Utf8Json files | 57 | 57 (guarded) | 0 |
| Vendored code lines | ~10,000 | ~10,000 (inactive) | 0 |
| Bridge code lines | 0 | ~800 | ~800 |
| Source files modified | 0 | 9 | 0 (deletions only) |
| IJsonFormatter<T> call sites changed | 0 | 0 | 0 |
| External API breakage | N/A | None | None |
| Minimum .NET version | netstandard2.0 | netstandard2.0 | netstandard2.0 |
