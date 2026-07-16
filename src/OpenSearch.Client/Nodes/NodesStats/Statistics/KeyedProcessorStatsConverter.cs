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
	/// Reads/writes the single-key wrapper object <c>{ "&lt;type&gt;": { ...stats... } }</c> used for each
	/// processor entry in node ingest stats. The property name is the processor <see cref="KeyedProcessorStats.Type"/>
	/// and the value deserializes into <see cref="KeyedProcessorStats.Statistics"/>. Replaces the pre-STJ
	/// <c>KeyedProcessorStatsFormatter</c>; without it the JSON shape is not mapped and <c>Type</c> stays null.
	/// </summary>
	internal sealed class KeyedProcessorStatsConverter : JsonConverter<KeyedProcessorStats>
	{
		public override KeyedProcessorStats Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var stats = new KeyedProcessorStats();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				stats.Type = reader.GetString();
				reader.Read();
				stats.Statistics = JsonSerializer.Deserialize<ProcessStats>(ref reader, options);
			}

			return stats;
		}

		public override void Write(Utf8JsonWriter writer, KeyedProcessorStats value, JsonSerializerOptions options)
		{
			if (value?.Type == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(value.Type);
			JsonSerializer.Serialize(writer, value.Statistics, options);
			writer.WriteEndObject();
		}
	}
}
