/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.Text.Json;
using System.Text.Json.Serialization;
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
				WriteIndented = writeIndented
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
			// TODO: Implement IsADictionaryConverterFactory to handle types implementing IsADictionaryBase<TKey, TValue>
			// options.Converters.Insert(0, new IsADictionaryConverterFactory(settings));

			// --- 4. ReadAs converters (interface → implementation mapping) ---
			options.Converters.Insert(0, new ReadAsConverterFactory());

			// --- 1. Explicit singleton converters (highest precedence among domain converters) ---
			// TODO: Uncomment as each converter is implemented in subsequent tasks

			// Query DSL converters (task 6.x)
			options.Converters.Insert(0, new QueryContainerConverter());
			options.Converters.Insert(0, new QueryContainerConcretConverter());
			options.Converters.Insert(0, new QueryContainerCollectionConverter());
			options.Converters.Insert(0, new TermsQueryConverter());
			options.Converters.Insert(0, new RangeQueryConverter());
			options.Converters.Insert(0, new GeoShapeQueryConverter());

			// Aggregation converters (task 8.x)
			options.Converters.Insert(0, new AggregationContainerConverter());
			options.Converters.Insert(0, new AggregationContainerInterfaceConverter());
			options.Converters.Insert(0, new AggregationDictionaryConverter());
			options.Converters.Insert(0, new AggregateDictionaryResponseConverter());
			options.Converters.Insert(0, new AggregateConverter());

			// Mapping converters (task 9.1)
			options.Converters.Insert(0, new PropertyConverter());

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

			return options;
		}
	}
}
