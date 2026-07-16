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
	/// Deserializes the <c>_source</c> filter, which may take the form of a single field string,
	/// an array of field strings (both treated as <c>includes</c>), or an object with
	/// <c>includes</c>/<c>excludes</c> arrays. Serializes as the object form. Replaces the Utf8Json
	/// <c>SourceFilterFormatter</c>.
	/// </summary>
	internal sealed class SourceFilterConverter : JsonConverter<ISourceFilter>
	{
		public override ISourceFilter Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
				{
					var name = reader.GetString();
					return new SourceFilter { Includes = new[] { name } };
				}
				case JsonTokenType.StartArray:
				{
					var includes = JsonSerializer.Deserialize<Fields>(ref reader, options);
					return new SourceFilter { Includes = includes };
				}
				case JsonTokenType.StartObject:
				{
					var filter = new SourceFilter();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
					{
						if (reader.TokenType != JsonTokenType.PropertyName)
							continue;

						var propertyName = reader.GetString();
						reader.Read();

						switch (propertyName)
						{
							case "includes":
								filter.Includes = JsonSerializer.Deserialize<Fields>(ref reader, options);
								break;
							case "excludes":
								filter.Excludes = JsonSerializer.Deserialize<Fields>(ref reader, options);
								break;
							default:
								reader.Skip();
								break;
						}
					}
					return filter;
				}
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, ISourceFilter value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			if (value.Excludes != null)
			{
				writer.WritePropertyName("excludes");
				JsonSerializer.Serialize(writer, value.Excludes, options);
			}
			if (value.Includes != null)
			{
				writer.WritePropertyName("includes");
				JsonSerializer.Serialize(writer, value.Includes, options);
			}
			writer.WriteEndObject();
		}
	}
}
