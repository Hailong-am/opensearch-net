/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="IProcessor"/> that handles
	/// the single-key object polymorphic dispatch pattern used by OpenSearch ingest pipelines.
	/// Each processor is serialized as <c>{"processor_name": { ... }}</c>.
	/// </summary>
	internal sealed class ProcessorConverter : JsonConverter<IProcessor>
	{
		/// <summary>
		/// Maps JSON processor name → concrete processor type for deserialization.
		/// </summary>
		private static readonly Dictionary<string, Type> ProcessorTypes = new(StringComparer.OrdinalIgnoreCase)
		{
			["attachment"] = typeof(AttachmentProcessor),
			["append"] = typeof(AppendProcessor),
			["convert"] = typeof(ConvertProcessor),
			["date"] = typeof(DateProcessor),
			["date_index_name"] = typeof(DateIndexNameProcessor),
			["dot_expander"] = typeof(DotExpanderProcessor),
			["fail"] = typeof(FailProcessor),
			["foreach"] = typeof(ForeachProcessor),
			["json"] = typeof(JsonProcessor),
			["user_agent"] = typeof(UserAgentProcessor),
			["kv"] = typeof(KeyValueProcessor),
			["geoip"] = typeof(GeoIpProcessor),
			["grok"] = typeof(GrokProcessor),
			["gsub"] = typeof(GsubProcessor),
			["join"] = typeof(JoinProcessor),
			["lowercase"] = typeof(LowercaseProcessor),
			["remove"] = typeof(RemoveProcessor),
			["rename"] = typeof(RenameProcessor),
			["script"] = typeof(ScriptProcessor),
			["set"] = typeof(SetProcessor),
			["sort"] = typeof(SortProcessor),
			["split"] = typeof(SplitProcessor),
			["trim"] = typeof(TrimProcessor),
			["uppercase"] = typeof(UppercaseProcessor),
			["urldecode"] = typeof(UrlDecodeProcessor),
			["bytes"] = typeof(BytesProcessor),
			["dissect"] = typeof(DissectProcessor),
			["pipeline"] = typeof(PipelineProcessor),
			["drop"] = typeof(DropProcessor),
			["csv"] = typeof(CsvProcessor),
			["uri_parts"] = typeof(UriPartsProcessor),
			["fingerprint"] = typeof(FingerprintProcessor),
			["community_id"] = typeof(NetworkCommunityIdProcessor),
			["network_direction"] = typeof(NetworkDirectionProcessor),
			["text_embedding"] = typeof(TextEmbeddingProcessor),
		};

		public override IProcessor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			// Read the first (and only) property name — this is the processor type key
			reader.Read();
			if (reader.TokenType != JsonTokenType.PropertyName)
				return null;

			var processorName = reader.GetString();
			reader.Read(); // Move to the value (the processor object)

			IProcessor processor = null;

			if (processorName != null && ProcessorTypes.TryGetValue(processorName, out var processorType))
			{
				processor = (IProcessor)JsonSerializer.Deserialize(ref reader, processorType, options);
			}
			else
			{
				// Unknown processor type — skip the value to maintain forward compatibility
				reader.Skip();
			}

			// Read past any additional properties and the end of the outer object
			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType == JsonTokenType.PropertyName)
				{
					reader.Read();
					reader.Skip();
				}
			}

			return processor;
		}

		public override void Write(Utf8JsonWriter writer, IProcessor value, JsonSerializerOptions options)
		{
			if (value?.Name == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(value.Name);
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
			writer.WriteEndObject();
		}
	}
}
