/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.Text.Encodings.Web;
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
				Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
				WriteIndented = writeIndented,
				TypeInfoResolver = new DefaultJsonTypeInfoResolver
				{
					Modifiers = { DataMemberPropertyNameModifier.Modify }
				}
			};

			// Converters registered in precedence order (first match wins).
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
