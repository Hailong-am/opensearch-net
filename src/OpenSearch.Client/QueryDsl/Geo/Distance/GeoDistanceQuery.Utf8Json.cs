/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using OpenSearch.Net.Extensions;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Client
{
	internal class GeoDistanceQueryFormatter : IJsonFormatter<IGeoDistanceQuery>
	{
		private static readonly AutomataDictionary Fields = new AutomataDictionary
		{
			{ "_name", 0 },
			{ "boost", 1 },
			{ "validation_method", 2 },
			{ "distance", 3 },
			{ "distance_type", 4 }
		};

		public IGeoDistanceQuery Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.GetCurrentJsonToken() != JsonToken.BeginObject)
				return null;

			var query = new GeoDistanceQuery();
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
							query.Distance = formatterResolver.GetFormatter<Distance>()
								.Deserialize(ref reader, formatterResolver);
							break;
						case 4:
							query.DistanceType = formatterResolver.GetFormatter<GeoDistanceType>()
								.Deserialize(ref reader, formatterResolver);
							break;
					}
				}
				else
				{
					query.Field = property.Utf8String();
					query.Location = formatterResolver.GetFormatter<GeoLocation>()
						.Deserialize(ref reader, formatterResolver);
				}
			}

			return query;
		}

		public void Serialize(ref JsonWriter writer, IGeoDistanceQuery value, IJsonFormatterResolver formatterResolver)
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

			if (value.Distance != null)
			{
				if (written)
					writer.WriteValueSeparator();

				writer.WritePropertyName("distance");
				formatterResolver.GetFormatter<Distance>()
					.Serialize(ref writer, value.Distance, formatterResolver);
				written = true;
			}

			if (value.DistanceType != null)
			{
				if (written)
					writer.WriteValueSeparator();

				writer.WritePropertyName("distance_type");
				formatterResolver.GetFormatter<GeoDistanceType>()
					.Serialize(ref writer, value.DistanceType.Value, formatterResolver);
				written = true;
			}

			if (written)
				writer.WriteValueSeparator();

			var settings = formatterResolver.GetConnectionSettings();
			writer.WritePropertyName(settings.Inferrer.Field(value.Field));
			formatterResolver.GetFormatter<GeoLocation>()
				.Serialize(ref writer, value.Location, formatterResolver);

			writer.WriteEndObject();
		}
	}

}
