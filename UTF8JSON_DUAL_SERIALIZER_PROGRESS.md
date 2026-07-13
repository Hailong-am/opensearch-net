# Utf8Json ⇄ System.Text.Json dual-serializer — progress & findings

## Goal
Keep the vendored Utf8Json engine alongside System.Text.Json (STJ, the default) and let
callers pick at runtime via `ConnectionSettings.UseUtf8Json()` or the `OSC_USE_UTF8JSON`
environment variable. STJ stays the default; Utf8Json is a rollback safety net.

## Design
- Domain types carry **both** `[JsonConverter]` (STJ) and `[JsonFormatter]`/`[ReadAs]`
  (Utf8Json). Each engine ignores the other's attributes.
- `DefaultHighLevelSerializer` selects the engine once at construction from
  `settings.UseUtf8Json`. STJ path unchanged; Utf8Json path builds an
  `OpenSearchClientFormatterResolver` and routes (de)serialization through the vendored
  `JsonSerializer`.
- `IInternalSerializer` exposes both `TryGetJsonSerializerOptions` (STJ) and
  `TryGetFormatterResolver` (Utf8Json); response builders/proxies branch on which returns true.

## Restored from pre-migration base (merge-base a94d09e)
- 7 Utf8Json resolvers + `StatefulSerializerExtensions` + `SourceValueWriteConverter`.
- ~90 standalone `*Formatter.cs` / `*JsonConverter.cs` files (incl. renamed ones).
- 38 inline formatter classes → extracted into `<Type>.Utf8Json.cs` companion files.
- Re-added `[JsonFormatter]`/`using OpenSearch.Net.Utf8Json;` to ~600 domain files (scripted).
- Re-added Utf8Json-only helper members removed during migration:
  `OpenSearchSerializerExtensions.SerializeUsingWriter`, `ArraySegmentBytesExtensions.IsDateTime`,
  `Inferrer.CreateMultiHitDelegates`/`CreateSearchResponseDelegates`, resolver-based
  `LazyDocument`/`TopHitsAggregate` ctors, `ResponseFormatterHelpers.ServerErrorFieldsAutomata`.

## Key fixes (root causes, not symptoms)
1. **InternalsVisibleTo** for the runtime-emitted Utf8Json dynamic-resolver assemblies
   (`DynamicCompositeResolver`, `DynamicObjectResolver*`) were dropped during migration —
   restored in both `OpenSearch.Net.csproj` and `OpenSearch.Client.csproj`. Without them the
   emitted resolvers failed to load ("Access is denied" / `TypeAccessException`).
2. **Two `InterfaceDataContractAttribute` types**: the migration added
   `OpenSearch.Client.InterfaceDataContractAttribute` (for the STJ modifier); domain interfaces
   now bind `[InterfaceDataContract]` to it, not the vendored
   `OpenSearch.Net.Utf8Json.InterfaceDataContractAttribute` that `MetaType` checked. `MetaType`
   saw `dataContractPresent=false` and serialized every public member (e.g. explicit
   `IAggregation.Name`). Fixed `MetaType` to recognize the client attribute by name.
   **-154 failures.**
3. **`IBoolQuery` lost `[ReadAs(typeof(BoolQuery))]`** (migration replaced it with
   `[JsonConverter]`). Utf8Json could not deserialize the interface → "generated serializer for
   IBoolQuery does not support deserialize" on every query-container round-trip. Re-added
   `[ReadAs]` alongside `[JsonConverter]`. **-10 failures.**

## Status (net10.0 test run) — BOTH MODES GREEN
- **STJ (default): 3249 passed / 0 failed / 5 skipped.**
- **Utf8Json (`OSC_USE_UTF8JSON=true`): 3249 passed / 0 failed / 5 skipped.**

## Additional root-cause fixes to reach parity (all "dual attribute / dual signature")
4. **`SerializationConstructorAttribute` also exists twice** (client + Utf8Json). Request types'
   non-public parameterless ctors bind to the client one, so Utf8Json `MetaType` missed them,
   fell back to `GetUninitializedObject`, and NRE'd deserializing requests. `MetaType` now
   recognizes the client attribute by name. **Fixed the ~15 request round-trip failures.**
5. **`IReindexDestination.Pipeline`** had no `[DataMember]` but the migration added
   `[InterfaceDataContract]`; under DataMember-only serialization the pipeline id was dropped.
   Added `[DataMember(Name = "pipeline")]`.
6. **Type-level `ShouldSerialize` hooks** (`QueryContainer`, `Routing`) were changed to STJ
   signatures (parameterless / `IConnectionSettingsValues`); Utf8Json only discovers a hook
   taking a single `IJsonFormatterResolver`. Added that overload to both, so conditionless
   queries and empty routing are omitted instead of emitted as `{"bool":{}}` / `"routing": null`.
7. **`ReadAsConverterFactory` (STJ) shadowed explicit `[JsonConverter]`**: adding `[ReadAs]` back
   to `IBoolQuery` made the STJ factory hijack it and regress STJ output. `CanConvert` now skips
   types that carry their own `[JsonConverter]`.

## How to reproduce a run
```
# default STJ
dotnet test tests/Tests/Tests.csproj -c Release
# Utf8Json engine
OSC_USE_UTF8JSON=true dotnet test tests/Tests/Tests.csproj -c Release
```
