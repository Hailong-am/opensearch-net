/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Provides per-<see cref="IConnectionSettingsValues"/> <see cref="JsonSerializerOptions"/> instances
	/// for the high-level OpenSearch.Client serializer.
	/// Replaces the Utf8Json-based <c>OpenSearchClientFormatterResolver</c>.
	/// </summary>
	/// <remarks>
	/// Inherits all base settings from <see cref="OpenSearchNetSerializerOptions.Instance"/> and
	/// adds high-level domain converters (query DSL, aggregations, mappings, ingest, etc.)
	/// in the correct precedence order.
	/// </remarks>
	internal class OpenSearchClientSerializerOptions
	{
		/// <summary>
		/// The <see cref="JsonSerializerOptions"/> instance for compact (non-indented) serialization.
		/// </summary>
		public JsonSerializerOptions Options { get; }

		/// <summary>
		/// The <see cref="JsonSerializerOptions"/> instance with <c>WriteIndented = true</c>,
		/// used when <see cref="SerializationFormatting.Indented"/> is requested.
		/// </summary>
		public JsonSerializerOptions Indented { get; }

		/// <summary>
		/// Creates a new instance of <see cref="OpenSearchClientSerializerOptions"/> configured
		/// for the given connection settings.
		/// </summary>
		/// <param name="settings">
		/// The connection settings providing property name inference and source serializer configuration.
		/// </param>
		public OpenSearchClientSerializerOptions(IConnectionSettingsValues settings)
		{
			Options = CreateOptions(settings, writeIndented: false);
			Indented = CreateOptions(settings, writeIndented: true);
		}

		private static JsonSerializerOptions CreateOptions(IConnectionSettingsValues settings, bool writeIndented)
		{
			// Copy from the base low-level options — this inherits PropertyNamingPolicy,
			// DefaultIgnoreCondition, NumberHandling, Encoder, TypeInfoResolver (with
			// DataMemberPropertyNameModifier that honors [DataMember(Name = "...")] attributes),
			// and base converters (DynamicDictionaryConverter, DynamicValueConverter,
			// EnumMemberConverterFactory, ExceptionConverter).
			var options = new JsonSerializerOptions(OpenSearchNetSerializerOptions.Instance)
			{
				WriteIndented = writeIndented,
				// Rebuild the resolver so that, in addition to the low-level [DataMember]/[IgnoreDataMember]
				// handling, the high-level [InterfaceDataContract] rebuild runs. The contract rebuild
				// makes request classes and descriptors serialize purely against their interface contract.
				TypeInfoResolver = new DefaultJsonTypeInfoResolver
				{
					Modifiers =
					{
						DataMemberPropertyNameModifier.Modify,
						InterfaceDataContractModifier.Modify
					}
				}
			};

			// High-level domain converters are inserted at position 0 so they take
			// precedence over the inherited base converters.
			// Converter resolution order (first match wins):
			//   1. Explicit singleton converters (domain-specific)
			//   2. Enum converter (EnumMemberConverterFactory) — inherited from base
			//   3. Attribute-based converters ([JsonConverter] on types) — handled by STJ automatically
			//   4. ReadAs converters (interface → implementation mapping)
			//   5. IsADictionary converters (for types implementing IsADictionaryBase)
			//   6. SourceConverterFactory (catch-all for user document types)

			// --- 6. SourceConverterFactory (lowest domain-converter precedence, inserted first) ---
			options.Converters.Insert(0, new SourceConverterFactory(settings));

			// --- 5. IsADictionary converters ---
			options.Converters.Insert(0, new IsADictionaryConverterFactory(settings));

			// --- 4. ReadAs converters (interface → implementation mapping) ---
			options.Converters.Insert(0, new ReadAsConverterFactory());

			// --- 1. Explicit singleton converters (highest precedence among domain converters) ---

			// Script converter (type-dispatch for IScript → IInlineScript/IIndexedScript)
			options.Converters.Insert(0, new ScriptConverter());

			// PropertyName converter (dictionary key support for IProperties and similar types)
			options.Converters.Insert(0, new PropertyNameConverter());

			// Field converter (resolves field names via Inferrer, handles boost/format)
			options.Converters.Insert(0, new FieldConverterFactory(settings));

			// IndexName converter (resolves index names via Inferrer)
			options.Converters.Insert(0, new IndexNameConverterFactory(settings));

			// RelationName converter (resolves relation/type names via Inferrer)
			options.Converters.Insert(0, new RelationNameConverterFactory(settings));

			// Id converter (resolves document IDs via Inferrer)
			options.Converters.Insert(0, new IdConverterFactory(settings));

			// Routing converter (resolves routing values via Inferrer)
			options.Converters.Insert(0, new RoutingConverterFactory(settings));

			// JoinField converter (parent-child relation join on user _source documents)
			options.Converters.Insert(0, new JoinFieldConverterFactory(settings));

			// Proxy request converter (Index/Create requests delegate their body to the source serializer)
			options.Converters.Insert(0, new ProxyRequestConverterFactory(settings));

			// MultiGet request converter (flattens to ids/docs and strips redundant per-doc index)
			options.Converters.Insert(0, new MultiGetRequestConverterFactory(settings));

			// Bulk request converter (NDJSON: action-metadata line + body line per operation)
			options.Converters.Insert(0, new BulkRequestConverterFactory(settings));

			// LazyDocument converter (captures raw JSON for deferred source/response deserialization)
			options.Converters.Insert(0, new LazyDocumentConverterFactory(settings));

			// Bulk response item converter (single-key operation dispatch on deserialize)
			options.Converters.Insert(0, new BulkResponseItemConverter());

			// Union converter (writes whichever member is set; reads TFirst then falls back to TSecond)
			options.Converters.Insert(0, new UnionConverterFactory());

			// Indices converter (multi-index string form: _all or comma-joined resolved names).
			// Inserted after UnionConverterFactory so it takes precedence: Indices derives from
			// Union<,> which the union factory would otherwise claim and emit as an object.
			options.Converters.Insert(0, new IndicesConverterFactory(settings));

			// Query DSL converters (task 6.x)
			options.Converters.Insert(0, new QueryContainerConverter());
			options.Converters.Insert(0, new QueryContainerConcretConverter());
			options.Converters.Insert(0, new QueryContainerCollectionConverter());
			options.Converters.Insert(0, new TermsQueryConverter());
			options.Converters.Insert(0, new RangeQueryConverter());

			// Field-name query wrapping (term, match, prefix, wildcard, fuzzy, regexp, span_term, etc.).
			// Adds the { "<resolved-field>": { ...body... } } wrapping around concrete field-name queries
			// that do not already have a dedicated converter. Inserted last so it takes precedence over
			// the InterfaceDataContractModifier's default object handling for these concrete types.
			options.Converters.Insert(0, new FieldNameQueryConverterFactory(settings));

			// Geo query converters (dedicated field-name-wrapping shapes). Inserted after the
			// field-name query factory so they take precedence for their specific interfaces
			// (the factory would otherwise claim them and emit the generic { field: { body } } wrapping).
			options.Converters.Insert(0, new GeoShapeQueryConverter(settings));
			options.Converters.Insert(0, new GeoBoundingBoxQueryConverter(settings));
			options.Converters.Insert(0, new GeoDistanceQueryConverter(settings));
			options.Converters.Insert(0, new GeoPolygonQueryConverter(settings));

			// Aggregation converters (task 8.x)
			options.Converters.Insert(0, new AggregationContainerConverter());
			options.Converters.Insert(0, new AggregationContainerInterfaceConverter());
			options.Converters.Insert(0, new AggregationDictionaryConverter());
			options.Converters.Insert(0, new AggregateDictionaryResponseConverter());
			options.Converters.Insert(0, new AggregateConverter());

			// Mapping converters (task 9.1)
			options.Converters.Insert(0, new PropertyConverter());

			// Sort converter (ISort → { "field": { ...body... } } dispatch)
			options.Converters.Insert(0, new SortConverter(settings));

			// Ingest pipeline converters (task 9.2)
			options.Converters.Insert(0, new ProcessorConverter());

			// Search response converters (task 10.x)
			options.Converters.Insert(0, new TotalHitsConverter());
			options.Converters.Insert(0, new InnerHitsMetadataConverter());
			options.Converters.Insert(0, new InnerHitsResultConverter());
			options.Converters.Insert(0, new HitConverterFactory());
			options.Converters.Insert(0, new HitsMetadataConverterFactory());
			options.Converters.Insert(0, new SuggestDictionaryConverterFactory());
			options.Converters.Insert(0, new SearchResponseConverterFactory());

			// Index settings converters (must precede IsADictionaryConverterFactory)
			options.Converters.Insert(0, new IndexSettingsConverter());
			options.Converters.Insert(0, new DynamicIndexSettingsConverter());

			return options;
		}
	}
}
