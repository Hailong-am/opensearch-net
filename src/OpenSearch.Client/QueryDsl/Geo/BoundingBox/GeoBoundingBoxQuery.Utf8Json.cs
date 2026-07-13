/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using OpenSearch.Net.Extensions;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Client
{
	internal class GeoBoundingBoxQueryFormatter : IJsonFormatter<IGeoBoundingBoxQuery>
	{
		private static readonly AutomataDictionary Fields = new AutomataDictionary
		{
			{ "_name", 0 },
			{ "boost", 1 },
			{ "validation_method", 2 },
			{ "type", 3 }
		};

		public IGeoBoundingBoxQuery Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.GetCurrentJsonToken() != JsonToken.BeginObject)
				return null;

			var query = new GeoBoundingBoxQuery();
			var count = 0;
			while (reader.ReadIsInObject(ref count))
			{
				var property = reader.ReadPropertyNameSegmentRaw();
				if (Fields.TryGetValue(property, out var value))
				{
					switch (value)
					{
						case 0:
							query.Name = reader.ReadString();
							break;
						case 1:
							query.Boost = reader.ReadDouble();
							break;
						case 2:
							query.ValidationMethod = formatterResolver.GetFormatter<GeoValidationMethod>()
								.Deserialize(ref reader, formatterResolver);
							break;
						case 3:
							query.Type = formatterResolver.GetFormatter<GeoExecution>()
								.Deserialize(ref reader, formatterResolver);
							break;
					}
				}
				else
				{
					query.Field = property.Utf8String();
					query.BoundingBox = formatterResolver.GetFormatter<IBoundingBox>()
						.Deserialize(ref reader, formatterResolver);
				}
			}

			return query;
		}

		public void Serialize(ref JsonWriter writer, IGeoBoundingBoxQuery value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			var written = false;

			writer.WriteBeginObject();

			if (!value.Name.IsNullOrEmpty())
			{
				writer.WritePropertyName("_name");
				writer.WriteString(value.Name);
				written = true;
			}

			if (value.Boost != null)
			{
				if (written)
					writer.WriteValueSeparator();

				writer.WritePropertyName("boost");
				writer.WriteDouble(value.Boost.Value);
				written = true;
			}

			if (value.ValidationMethod != null)
			{
				if (written)
					writer.WriteValueSeparator();

				writer.WritePropertyName("validation_method");
				formatterResolver.GetFormatter<GeoValidationMethod>()
					.Serialize(ref writer, value.ValidationMethod.Value, formatterResolver);
				written = true;
			}

			if (value.Type != null)
			{
				if (written)
					writer.WriteValueSeparator();

				writer.WritePropertyName("type");
				formatterResolver.GetFormatter<GeoExecution>()
					.Serialize(ref writer, value.Type.Value, formatterResolver);
				written = true;
			}

			if (written)
				writer.WriteValueSeparator();

			var settings = formatterResolver.GetConnectionSettings();
			writer.WritePropertyName(settings.Inferrer.Field(value.Field));
			formatterResolver.GetFormatter<IBoundingBox>()
				.Serialize(ref writer, value.BoundingBox, formatterResolver);

			writer.WriteEndObject();
		}
	}

}
