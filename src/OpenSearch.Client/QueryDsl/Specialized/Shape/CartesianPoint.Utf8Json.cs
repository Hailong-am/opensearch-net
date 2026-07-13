/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.IO;
using System.Text;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;
using OpenSearch.Client;
using OpenSearch.Net.Extensions;

namespace OpenSearch.Client
{
	internal class CartesianPointFormatter : IJsonFormatter<CartesianPoint>
	{
		private static readonly AutomataDictionary Fields = new AutomataDictionary { { "x", 0 }, { "y", 1 }, { "z", 2 } };

		public void Serialize(ref JsonWriter writer, CartesianPoint value, IJsonFormatterResolver formatterResolver)
		{
			if (value is null)
			{
				writer.WriteNull();
				return;
			}

			switch (value.Format)
			{
				case ShapeFormat.Object:
					writer.WriteBeginObject();
					writer.WritePropertyName("x");
					writer.WriteSingle(value.X);
					writer.WriteValueSeparator();
					writer.WritePropertyName("y");
					writer.WriteSingle(value.Y);
					writer.WriteEndObject();
					break;
				case ShapeFormat.Array:
					writer.WriteBeginArray();
					writer.WriteSingle(value.X);
					writer.WriteValueSeparator();
					writer.WriteSingle(value.Y);
					writer.WriteEndArray();
					break;
				case ShapeFormat.WellKnownText:
					writer.WriteQuotation();
					writer.WriteRaw(Encoding.UTF8.GetBytes(GeoShapeType.Point));
					writer.WriteRaw((byte)' ');
					writer.WriteRaw((byte)'(');
					writer.WriteSingle(value.X);
					writer.WriteRaw((byte)' ');
					writer.WriteSingle(value.Y);
					writer.WriteRaw((byte)')');
					writer.WriteQuotation();
					break;
				case ShapeFormat.String:
					writer.WriteQuotation();
					writer.WriteSingle(value.X);
					writer.WriteValueSeparator();
					writer.WriteSingle(value.Y);
					writer.WriteQuotation();
					break;
			}
		}

		public CartesianPoint Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();
			switch (token)
			{
				case JsonToken.BeginObject:
				{
					var count = 0;
					var point = new CartesianPoint { Format = ShapeFormat.Object };
					while (reader.ReadIsInObject(ref count))
					{
						var property = reader.ReadPropertyNameSegmentRaw();
						if (Fields.TryGetValue(property, out var value))
						{
							switch (value)
							{
								case 0:
									point.X = reader.ReadSingle();
									break;
								case 1:
									point.Y = reader.ReadSingle();
									break;
								case 2:
									reader.ReadSingle();
									break;
							}
						}
						else
							throw new JsonParsingException($"Unknown property {property.Utf8String()} when parsing {nameof(CartesianPoint)}");
					}

					return point;
				}
				case JsonToken.BeginArray:
				{
					var count = 0;
					var point = new CartesianPoint { Format = ShapeFormat.Array };
					while (reader.ReadIsInArray(ref count))
					{
						switch (count)
						{
							case 1:
								point.X = reader.ReadSingle();
								break;
							case 2:
								point.Y = reader.ReadSingle();
								break;
							case 3:
								reader.ReadSingle();
								break;
							default:
								throw new JsonParsingException($"Expected 2 or 3 coordinates but found {count}");
						}
					}

					return point;
				}
				case JsonToken.String:
				{
					var value = reader.ReadString();
					return value.IndexOf(",", StringComparison.InvariantCultureIgnoreCase) > -1
						? CartesianPoint.FromCoordinates(value)
						: CartesianPoint.FromWellKnownText(value);
				}
				default:
					throw new JsonParsingException($"Unexpected token type {token} when parsing {nameof(CartesianPoint)}");
			}
		}
	}

}
