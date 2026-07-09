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
	internal sealed class TermsIncludeConverter : JsonConverter<TermsInclude>
	{
		public override TermsInclude Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					reader.Skip();
					return null;
				case JsonTokenType.StartArray:
					var values = JsonSerializer.Deserialize<IEnumerable<string>>(ref reader, options);
					return new TermsInclude(values);
				case JsonTokenType.StartObject:
					long partition = 0;
					long numberOfPartitions = 0;
					while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
					{
						if (reader.TokenType != JsonTokenType.PropertyName)
							continue;
						var propertyName = reader.GetString();
						reader.Read();
						switch (propertyName)
						{
							case "partition":
								partition = reader.GetInt64();
								break;
							case "num_partitions":
								numberOfPartitions = reader.GetInt64();
								break;
							default:
								reader.Skip();
								break;
						}
					}
					return new TermsInclude(partition, numberOfPartitions);
				case JsonTokenType.String:
					return new TermsInclude(reader.GetString());
				default:
					throw new JsonException($"Unexpected token {reader.TokenType} when deserializing {nameof(TermsInclude)}");
			}
		}

		public override void Write(Utf8JsonWriter writer, TermsInclude value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
			}
			else if (value.Values != null)
			{
				JsonSerializer.Serialize(writer, value.Values, options);
			}
			else if (value.Partition.HasValue && value.NumberOfPartitions.HasValue)
			{
				writer.WriteStartObject();
				writer.WriteNumber("partition", value.Partition.Value);
				writer.WriteNumber("num_partitions", value.NumberOfPartitions.Value);
				writer.WriteEndObject();
			}
			else
			{
				writer.WriteStringValue(value.Pattern);
			}
		}
	}
}
