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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IShapeQuery"/> that handles
	/// the field-name-wrapping pattern with nested shape/indexed_shape properties.
	/// <para>
	/// The OpenSearch shape query JSON shape is:
	/// <code>
	/// {
	///   "_name": "...",
	///   "boost": 1.0,
	///   "ignore_unmapped": false,
	///   "field_name": {
	///     "shape": { "type": "...", "coordinates": [...] },
	///     "relation": "intersects"
	///   }
	/// }
	/// </code>
	/// or with indexed shape:
	/// <code>
	/// {
	///   "field_name": {
	///     "indexed_shape": { "id": "...", "index": "...", "path": "..." },
	///     "relation": "intersects"
	///   }
	/// }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class ShapeQueryConverter : JsonConverter<IShapeQuery>
	{
		public override IShapeQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			string fieldName = null;
			double? boost = null;
			string name = null;
			bool? ignoreUnmapped = null;
			IGeoShape shape = null;
			IFieldLookup indexedShape = null;
			ShapeRelation? relation = null;

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var propertyName = reader.GetString();
				reader.Read(); // Move to value

				switch (propertyName)
				{
					case "boost":
						boost = reader.GetDouble();
						break;
					case "_name":
						name = reader.GetString();
						break;
					case "ignore_unmapped":
						ignoreUnmapped = reader.GetBoolean();
						break;
					default:
						// This is the field name — its value is the shape parameters object
						fieldName = propertyName;
						ReadShapeBody(ref reader, options, out shape, out indexedShape, out relation);
						break;
				}
			}

			if (fieldName == null)
				return null;

			return new ShapeQuery
			{
				Field = fieldName,
				Boost = boost,
				Name = name,
				IgnoreUnmapped = ignoreUnmapped,
				Shape = shape,
				IndexedShape = indexedShape,
				Relation = relation
			};
		}

		public override void Write(Utf8JsonWriter writer, IShapeQuery value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var fieldName = value.Field?.ToString();
			if (string.IsNullOrEmpty(fieldName))
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (!string.IsNullOrEmpty(value.Name))
			{
				writer.WritePropertyName("_name");
				writer.WriteStringValue(value.Name);
			}

			if (value.Boost.HasValue)
			{
				writer.WritePropertyName("boost");
				writer.WriteNumberValue(value.Boost.Value);
			}

			if (value.IgnoreUnmapped.HasValue)
			{
				writer.WritePropertyName("ignore_unmapped");
				writer.WriteBooleanValue(value.IgnoreUnmapped.Value);
			}

			writer.WritePropertyName(fieldName);
			writer.WriteStartObject();

			if (value.Shape != null)
			{
				writer.WritePropertyName("shape");
				JsonSerializer.Serialize(writer, value.Shape, options);
			}
			else if (value.IndexedShape != null)
			{
				writer.WritePropertyName("indexed_shape");
				JsonSerializer.Serialize(writer, value.IndexedShape, options);
			}

			if (value.Relation.HasValue)
			{
				writer.WritePropertyName("relation");
				JsonSerializer.Serialize(writer, value.Relation.Value, options);
			}

			writer.WriteEndObject(); // end field body
			writer.WriteEndObject(); // end outer
		}

		private static void ReadShapeBody(ref Utf8JsonReader reader, JsonSerializerOptions options,
			out IGeoShape shape, out IFieldLookup indexedShape, out ShapeRelation? relation)
		{
			shape = null;
			indexedShape = null;
			relation = null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return;
			}

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var prop = reader.GetString();
				reader.Read();

				switch (prop)
				{
					case "shape":
						shape = JsonSerializer.Deserialize<IGeoShape>(ref reader, options);
						break;
					case "indexed_shape":
						indexedShape = JsonSerializer.Deserialize<IFieldLookup>(ref reader, options);
						break;
					case "relation":
						relation = JsonSerializer.Deserialize<ShapeRelation>(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}
		}
	}
}
