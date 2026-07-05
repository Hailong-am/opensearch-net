# opensearch-net STJ Migration -- Follow-up Plan & Change Volume

Companion to `docs/MIGRATION_CHECKLIST.md`. This document quantifies exactly what
remains after Phase 1 (bridge layer, already committed as `79dc73162`).

## opensearch-net 2.0 TFM Changes

The 2.0 major version drops EOL frameworks:

| Removed | Reason |
|---|---|
| .NET Framework < 4.7.2 | EOL, security concerns |
| .NET 5 | EOL since Nov 2022 |
| .NET 6 | EOL since Nov 2024 |
| `netstandard2.1` | Superseded by net8.0 target |

**Revised TFM matrix for 2.0:**

| Library | 1.x TFMs | 2.0 TFMs |
|---|---|---|
| OpenSearch.Net | netstandard2.0; netstandard2.1; net6.0; net8.0; net10.0 | **netstandard2.0; net8.0; net10.0** |
| OpenSearch.Client | netstandard2.0; netstandard2.1 | **netstandard2.0; net8.0** |

**What this unlocks for STJ:**

- `net8.0` target provides full `IJsonTypeInfoResolver.Modifiers`, source generators,
  `JsonDerivedType`, `Utf8JsonReader.CopyString`, and all modern STJ APIs
- `netstandard2.0` retained for .NET Framework 4.7.2+ consumers -- gets STJ 8.x via
  NuGet (most features work except source generators and some perf-only APIs)
- Conditional compile `#if NET8_0_OR_GREATER` enables the efficient modifier path on
  modern runtimes; netstandard2.0 falls back to reflection-based `JsonConverter<T>`
- No more `#if NET6_0` or `#if NETSTANDARD2_1` polyfill paths needed

**Impact on Phase 3 (propertyMapper):** Strategy A (`IJsonTypeInfoResolver.Modifiers`)
is now viable with conditional compile -- net8.0 uses modifiers for zero-reflection
property mapping; netstandard2.0 uses `JsonConverterFactory` fallback. This was
previously blocked because OpenSearch.Client only targeted netstandard2.0/2.1.

---

## Current State (Phase 1 -- DONE)

| Item | Value |
|---|---|
| Commit | `79dc73162` |
| Files changed | 15 |
| Lines added | +1358 |
| Behavioral equivalence tests | 14/14 pass |
| Compile errors introduced | 0 (both modes, all TFMs) |

---

## Phase 2: Enable Bridge by Default + Delete Vendored Code

### Precise file/line inventory

| Action | Target | Count |
|---|---|---|
| Delete | `src/OpenSearch.Net/Utf8Json/**/*.cs` (57 files) | ~20,559 lines |
| Delete | `src/OpenSearch.Net/Utf8Json/**/*.tt` (T4 templates: PrimitiveFormatter, ValueTupleFormatter, TupleFormatter, UnsafeMemory) | 4 files |
| Update | `OpenSearch.Net.csproj` -- remove `<None Update="Utf8Json\...">` T4 generator entries | 4 ItemGroup blocks |
| Update | `OpenSearch.Net.csproj` -- remove `InternalsVisibleTo` entries for `OpenSearch.Net.CustomDynamicObjectResolver`, `OpenSearch.Net.DynamicCompositeResolver`, `OpenSearch.Net.DynamicObjectResolverAllowPrivate*` (7 entries, all dead once DynamicObjectResolver is gone) | 7 lines |
| Update | Remove all `#if !USE_STJ_BRIDGE` / `#else` / `#endif` markers (7 files from Phase 1) -- code becomes unconditional bridge-only | 7 files, ~20 markers |
| Update | `tests/Tests/CodeStandards/NamingConventions.doc.cs` -- remove/update assertions referencing `OpenSearch.Net.Utf8Json` types | 1 file, TBD lines (needs read) |
| Update | `tests/Tests/CodeStandards/Serialization/Formatters.doc.cs` -- remove/update assertions enumerating `IJsonFormatter<T>` implementers via reflection over the old namespace | 1 file, TBD lines (needs read) |
| Verify | Run full integration test suite (`tests/Tests`) against a live ephemeral OpenSearch cluster -- NOT run yet (this environment has no cluster binary) | N/A -- requires CI or a machine with cluster access |
| Add | Performance benchmark project using BenchmarkDotNet comparing serialize/deserialize throughput + allocations, old vs bridge | 1 new project (~150-200 lines) |

### Phase 2 net change estimate

| Metric | Estimate |
|---|---|
| Files deleted | 61 (57 .cs + 4 .tt) |
| Lines deleted | ~20,600 |
| Files modified | 10 (csproj + 7 bridge-adjacent files losing `#if` + 2 CodeStandards test docs) |
| Lines modified (removing `#if` noise) | ~40 |
| New files (benchmark project) | 1 project, ~150-200 lines |
| **Net line delta** | **-20,000 approx (net deletion)** |

### Phase 2 sequencing (must happen in this order)

1. Get Phase 1 PR merged and soak for >= 1 release cycle with `USE_STJ_BRIDGE` opt-in (community can test)
2. Run integration test suite with `USE_STJ_BRIDGE=true` on CI (requires ephemeral cluster -- GitHub Actions runner, not local)
3. Fix any integration test failures found (unknown count until run -- the 14 unit-level equivalence tests don't cover network/cluster serialization edge cases like streaming bulk responses, error deserialization, or aggregation polymorphic containers)
4. Run BenchmarkDotNet comparison; if regression >20% on hot paths, profile and optimize bridge (see Phase 3 optimizations) before flipping default
5. Flip default: add `<DefineConstants>USE_STJ_BRIDGE</DefineConstants>` unconditionally in `OpenSearch.Net.csproj`
6. One release cycle with bridge as default but vendored code still present (rollback safety net)
7. Delete vendored `Utf8Json/` directory + all `#if` markers
8. Update `docs/MIGRATION_CHECKLIST.md` and `AGENTS.md` to remove "migration incomplete" warnings

---

## Phase 3: Deep Modernization (Optional, Post-Phase 2)

These are NOT required for the migration to be "done" -- they're quality/performance
follow-ups once the bridge is the only code path.

| Item | Description | Estimated effort |
|---|---|---|
| Replace `PooledBufferWriter` polyfill | Since netstandard2.0 is retained in 2.0 (for .NET Framework 4.7.2+), polyfill stays until a future 3.0 drops netstandard2.0. Alternatively, adopt `Microsoft.Bcl.Memory` NuGet to get `ArrayBufferWriter<T>` on netstandard2.0 | 1 file, -50 lines (if Bcl.Memory adopted) |
| `DynamicObjectResolverShim` -> real `JsonTypeInfoResolver.Modifiers` | Current shim uses `JsonSerializer.Serialize<T>(value, options)` generically. With net8.0 as a first-class target in 2.0, use `IJsonTypeInfoResolver` with modifiers to replicate `propertyMapper` semantics. Conditional compile: net8.0 uses modifiers (zero-reflection), netstandard2.0 falls back to `JsonConverterFactory` | 1 file rewrite, ~150-200 lines. **No longer blocked** -- net8.0 target available in 2.0 |
| Drop `netstandard2.0` TFM (future 3.0) | Unlocks `JsonTypeInfoResolver` unconditionally, native `ArrayBufferWriter`, range operators, and other APIs used awkwardly via `#if NETSTANDARD2_0` polyfills today. This is a breaking change for consumers still on .NET Framework 4.7.2 | Requires community RFC / major version bump (3.0) -- NOT a code change estimate, a policy decision |
| Migrate 168 cross-project `IJsonFormatter<T>` implementations to `JsonConverter<T>` | The bridge lets these stay as-is indefinitely. This step is the "real" interface migration -- rewriting each formatter's `Serialize`/`Deserialize` body to use `Utf8JsonWriter`/`Utf8JsonReader` natively instead of through the bridge shim. Can be done module-by-module (see breakdown below), each is an independent PR | See table below |
| Migrate 202 `OpenSearch.Net`-internal `IJsonFormatter<T>` implementations | Same as above but for low-level client internals (already excluded from Phase 1/2 since they're wrapped by the bridge, but true modernization removes the wrapper) | Bundled with Phase 2 deletion -- these live in the deleted `Utf8Json/Formatters/` |
| Source-generator support (`[JsonSerializable]`) for AOT/trimming | Add `JsonSerializerContext` partial classes so consumers can trim/AOT-compile. Currently `DynamicObjectResolverShim` uses reflection-based `JsonSerializer.Serialize<T>` which blocks trimming | New partial class per major domain type cluster, ~20-30 files |

### Phase 3 `OpenSearch.Client` formatter migration breakdown (168 refs, by module)

If choosing to migrate formatters off the bridge (fully optional -- bridge works indefinitely):

| Module | Files with `IJsonFormatter<T>` | Suggested PR grouping |
|---|---|---|
| `CommonAbstractions` | 38 | PR 1 (core primitives: Field, IndexName, PropertyName, Union, LazyDocument formatters -- foundational, do first) |
| `QueryDsl` | 19 | PR 2 |
| `Aggregations` | 14 | PR 3 |
| `Mapping` | 10 | PR 4 |
| `CommonOptions` | 9 | PR 5 |
| `Search` | 8 | PR 6 |
| `Document` | 7 | PR 7 |
| `Indices` | 6 | PR 8 (can combine with PR 4) |
| `Analysis` | 6 | PR 8 (can combine with PR 4) |
| `Snapshot` | 3 | PR 9 (small, combine with Nodes/Ingest/Cluster/Cat) |
| `Nodes`, `Ingest`, `Cluster`, `Cat` | 1 each (4 total) | PR 9 |
| **Total** | **124 files** (some files have multiple `IJsonFormatter<T>` implementations, hence 168 refs > 124 files) | 9 independently-mergeable PRs |

Each PR in this table is optional and can ship independently, in any order, with no
cross-dependencies -- because the bridge means un-migrated formatters keep working
via `IJsonFormatter<T>` regardless of how many sibling formatters have been converted
to native `JsonConverter<T>`.

---

## Total Change Volume Summary (All Phases)

| Phase | Files touched | Lines added | Lines deleted | Net | Status |
|---|---|---|---|---|---|
| **Phase 1** (bridge layer) | 15 | +1358 | 0 | +1358 | ✅ DONE (commit `79dc73162`) |
| **Phase 2** (delete vendored, enable default) | ~71 (61 deleted + 10 modified) | ~150-200 (benchmark project) | ~20,640 | **~-20,450** | Pending -- needs CI cluster access |
| **Phase 3** (optional formatter modernization) | Up to 124 (if fully done) | Rewrite in-place, net ~0 (converter bodies replace formatter bodies 1:1) | Rewrite in-place | ~0 net (pure rewrite) | Optional, no deadline |
| **Grand total (Phase 1+2)** | ~86 files | ~1550 | ~20,640 | **~-19,090** | Repo shrinks by ~19K lines once vendored code is removed |

### Key insight on volume

The bridge design means **Phase 1 is the only phase requiring hand-written new code**
(~800 lines of bridge logic). Phase 2 is almost entirely **deletion** (the vendored
Utf8Json fork, ~20K lines, becomes dead code once nothing references it). Phase 3 is
**optional rewriting-in-place** with no net addition -- and can be deferred indefinitely
without blocking anything, since the bridge is a complete, permanent solution on its own
if the team decides not to pursue full native STJ converters.

---

## Immediate Next Actions (ranked)

1. **Open GitHub PR for Phase 1** against `opensearch-project/opensearch-net` (main blocker: needs review from OpenSearch .NET maintainers)
2. **Read the 2 CodeStandards doc tests** (`NamingConventions.doc.cs`, `Formatters.doc.cs`) to scope exactly what Phase 2 needs to change there -- currently unknown line count, flagged as TBD above
3. **Set up CI cluster access** (GitHub Actions with ephemeral OpenSearch) to actually run the integration test suite in bridge mode -- this environment can't run it locally (no OpenSearch binary)
4. **Write the BenchmarkDotNet comparison project** -- needed before Phase 2 step 4 (performance gate)
