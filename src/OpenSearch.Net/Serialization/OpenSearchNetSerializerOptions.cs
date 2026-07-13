/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace OpenSearch.Net
{
	/// <summary>
	/// Provides pre-configured <see cref="JsonSerializerOptions"/> instances for the low-level
	/// OpenSearch.Net serializer. Replaces the Utf8Json-based <c>OpenSearchNetFormatterResolver</c>.
	/// </summary>
	internal static class OpenSearchNetSerializerOptions
	{
		/// <summary>
		/// The default <see cref="JsonSerializerOptions"/> instance used for compact (non-indented) serialization.
		/// This instance is reused across all serialization calls for performance.
		/// </summary>
		public static JsonSerializerOptions Instance { get; } = Create(writeIndented: false);

		/// <summary>
		/// A <see cref="JsonSerializerOptions"/> instance configured with <c>WriteIndented = true</c>,
		/// used when <see cref="SerializationFormatting.Indented"/> is requested.
		/// </summary>
		public static JsonSerializerOptions Indented { get; } = Create(writeIndented: true);

		private static JsonSerializerOptions Create(bool writeIndented)
		{
			var options = new JsonSerializerOptions
			{
				PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
				DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
				NumberHandling = JsonNumberHandling.AllowReadingFromString,
				PropertyNameCaseInsensitive = true,
				Encoder = MinimalJsonEscapingEncoder.Shared,
				WriteIndented = writeIndented,
				TypeInfoResolver = new DefaultJsonTypeInfoResolver
				{
					Modifiers = { DataMemberPropertyNameModifier.Modify }
				}
			};

			// Converters registered in precedence order (first match wins).
			// Lenient bool converters: accept string "true"/"false" in addition to native
			// JSON booleans. OpenSearch may return boolean fields as strings in some responses.
			options.Converters.Add(new LenientBoolConverter());
			options.Converters.Add(new NullableLenientBoolConverter());
			// Fractional number converters: emit integral doubles/floats/decimals with a
			// trailing ".0" (e.g. 1.0 not 1), matching the client's historical wire format.
			options.Converters.Add(new DoubleConverter());
			options.Converters.Add(new SingleConverter());
			options.Converters.Add(new DecimalConverter());
			// ISO-8601 date converters: accept OpenSearch's flexible offset (+1000/+10) and
			// variable fractional-second forms that STJ's built-in readers reject.
			options.Converters.Add(new IsoDateTimeConverter());
			options.Converters.Add(new IsoDateTimeOffsetConverter());
			// TimeSpan serialized as ticks (a long), matching the client's historical wire format.
			options.Converters.Add(new TimeSpanTicksConverter());
			options.Converters.Add(new NullableTimeSpanTicksConverter());
			options.Converters.Add(new ErrorConverter());
			options.Converters.Add(new ErrorCauseConverter());
			options.Converters.Add(new DynamicDictionaryConverter());
			options.Converters.Add(new DynamicValueConverter());
			options.Converters.Add(new ObjectConverter());
			options.Converters.Add(new EnumMemberConverterFactory());
			options.Converters.Add(new ExceptionConverter());

			return options;
		}
	}
}
