/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes an <see cref="ICompositeAggregationSource"/> as
	/// <c>{ "&lt;name&gt;": { "&lt;source_type&gt;": { ...body } } }</c>. The body is written using the
	/// concrete runtime type's own interface-data-contract, so this converter is only applied via the
	/// interface attribute channel to avoid infinite recursion. Replaces the Utf8Json
	/// <c>CompositeAggregationSourceFormatter</c>.
	/// </summary>
	internal sealed class CompositeAggregationSourceConverter : JsonConverter<ICompositeAggregationSource>
	{
		public override ICompositeAggregationSource Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			// { name: { source_type: { body } } }
			reader.Read(); // property name (name)
			var name = reader.GetString();
			reader.Read(); // start object (source wrapper)
			reader.Read(); // property name (source type)
			var sourceType = reader.GetString();
			reader.Read(); // start object (body)

			ICompositeAggregationSource source = sourceType switch
			{
				"terms" => JsonSerializer.Deserialize<TermsCompositeAggregationSource>(ref reader, options),
				"date_histogram" => JsonSerializer.Deserialize<DateHistogramCompositeAggregationSource>(ref reader, options),
				"histogram" => JsonSerializer.Deserialize<HistogramCompositeAggregationSource>(ref reader, options),
				"geotile_grid" => JsonSerializer.Deserialize<GeoTileGridCompositeAggregationSource>(ref reader, options),
				_ => throw new JsonException($"Unknown {nameof(ICompositeAggregationSource)}: {sourceType}")
			};

			// After deserializing the body the reader is on the body's EndObject. Advance past the
			// source-wrapper EndObject and onto this value's own outer EndObject, where a converter
			// must leave the reader positioned.
			reader.Read(); // end object (source wrapper)
			reader.Read(); // end object (outer)

			if (source != null)
				source.Name = name;

			return source;
		}

		public override void Write(Utf8JsonWriter writer, ICompositeAggregationSource value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(value.Name);
			writer.WriteStartObject();
			writer.WritePropertyName(value.SourceType);
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
			writer.WriteEndObject();
			writer.WriteEndObject();
		}
	}
}
