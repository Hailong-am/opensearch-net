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

## Status (net10.0 test run)
- **STJ (default): green** — full suite unchanged from before this work.
- **Utf8Json (`OSC_USE_UTF8JSON=true`): 3235 / 3254 passing (99.4%).**
  Serialization (the production direction) verified correct via standalone probes.

## Remaining 19 Utf8Json-mode failures (3 root causes)
1. **Request round-trip deserialize NRE** (~15 tests: CreateIndex/Clone/Shrink/Split,
   ReindexOnServer, BulkInvalid). Deserializing a *request* type (e.g. `ICreateIndexRequest`)
   throws `NullReferenceException` even for `{}`. The emitted Utf8Json formatter builds the
   request via `RuntimeHelpers.GetUninitializedObject` (no parameterless ctor), leaving request
   base state null. STJ round-trips these fine. Deserializing requests is a test-only scenario
   (requests are only ever serialized in production).
2. **Conditionless query serialization** (`SearchApiNullQueryContainerTests`): Utf8Json emits
   `{"query":{"bool":{}}}` where STJ (and the expectation) omit the empty query.
3. **Source-serializer bulk ordering/content** (`SendsUsingSourceSerializer.BulkRequest`,
   `MultiTermVectorsRequest`): a small diff in one of several compared items.

## How to reproduce a run
```
# default STJ
dotnet test tests/Tests/Tests.csproj -c Release
# Utf8Json engine
OSC_USE_UTF8JSON=true dotnet test tests/Tests/Tests.csproj -c Release
```
