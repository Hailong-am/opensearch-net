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
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Handles serialization and deserialization of <see cref="ISort"/> instances.
	/// <para>
	/// Write format: <c>{ "sort_key": { ...body... } }</c>
	/// </para>
	/// <para>
	/// Read format:
	/// <list type="bullet">
	/// <item><c>"string"</c> → <see cref="FieldSort"/> with <c>Field = string</c></item>
	/// <item><c>{ "_script": { ... } }</c> → <see cref="ScriptSort"/></item>
	/// <item><c>{ "_geo_distance": { ... } }</c> → <see cref="GeoDistanceSort"/></item>
	/// <item><c>{ "field": "asc"/"desc" }</c> → <see cref="FieldSort"/> with Order</item>
	/// <item><c>{ "field": { ... } }</c> → <see cref="FieldSort"/></item>
	/// </list>
	/// </para>
	/// </summary>
	internal sealed class SortConverter : JsonConverter<ISort>
	{
		private readonly IConnectionSettingsValues _settings;

		public SortConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override ISort Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			// Simple string form: "fieldName"
			if (reader.TokenType == JsonTokenType.String)
			{
				var field = reader.GetString();
				return new FieldSort { Field = field };
			}

			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Cannot deserialize ISort from {reader.TokenType}");

			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			// Single-key object dispatch
			foreach (var prop in root.EnumerateObject())
			{
				var key = prop.Name;

				if (key == "_script")
				{
					var scriptSort = JsonSerializer.Deserialize<ScriptSort>(prop.Value.GetRawText(), options);
					return scriptSort;
				}

				if (key == "_geo_distance")
				{
					return DeserializeGeoDistanceSort(prop.Value, options);
				}

				// Default: field sort
				if (prop.Value.ValueKind == JsonValueKind.String)
				{
					// { "field": "asc" } shorthand
					var orderStr = prop.Value.GetString();
					SortOrder? order = null;
					if (string.Equals(orderStr, "asc", StringComparison.OrdinalIgnoreCase))
						order = SortOrder.Ascending;
					else if (string.Equals(orderStr, "desc", StringComparison.OrdinalIgnoreCase))
						order = SortOrder.Descending;

					return new FieldSort { Field = key, Order = order };
				}

				// { "field": { ...body... } }
				var fieldSort = JsonSerializer.Deserialize<FieldSort>(prop.Value.GetRawText(), options);
				if (fieldSort != null)
					fieldSort.Field = key;
				return fieldSort;
			}

			return null;
		}

		public override void Write(Utf8JsonWriter writer, ISort value, JsonSerializerOptions options)
		{
			if (value?.SortKey == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			var sortKey = value.SortKey.Name ?? _settings.Inferrer.Field(value.SortKey);

			switch (sortKey)
			{
				case "_script":
					writer.WritePropertyName("_script");
					WriteBodyWithoutSortKey<IScriptSort>(writer, value as IScriptSort, options);
					break;

				case "_geo_distance":
					writer.WritePropertyName("_geo_distance");
					WriteGeoDistanceBody(writer, value as IGeoDistanceSort, options);
					break;

				default:
					// Field sort: { "resolved_field_name": { ...body... } }
					var resolvedField = _settings.Inferrer.Field(value.SortKey);
					writer.WritePropertyName(resolvedField);
					WriteBodyWithoutSortKey<IFieldSort>(writer, value as IFieldSort, options);
					break;
			}

			writer.WriteEndObject();
		}

		private void WriteBodyWithoutSortKey<T>(Utf8JsonWriter writer, T value, JsonSerializerOptions options) where T : class, ISort
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// Serialize the body properties using the interface contract (which excludes SortKey via [IgnoreDataMember])
			JsonSerializer.Serialize(writer, value, typeof(T), options);
		}

		private void WriteGeoDistanceBody(Utf8JsonWriter writer, IGeoDistanceSort value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// GeoDistanceSort serialization is special: the field name with points array is
			// injected INSIDE the body alongside normal properties.
			// Output: { "distance_type": "...", "unit": "...", ..., "<field>": [...points...] }
			writer.WriteStartObject();

			// Write standard properties
			if (value.DistanceType.HasValue)
			{
				writer.WritePropertyName("distance_type");
				JsonSerializer.Serialize(writer, value.DistanceType.Value, options);
			}
			if (value.Unit.HasValue)
			{
				writer.WritePropertyName("unit");
				JsonSerializer.Serialize(writer, value.Unit.Value, options);
			}
			if (value.IgnoreUnmapped.HasValue)
			{
				writer.WritePropertyName("ignore_unmapped");
				writer.WriteBooleanValue(value.IgnoreUnmapped.Value);
			}
			if (value.Order.HasValue)
			{
				writer.WritePropertyName("order");
				JsonSerializer.Serialize(writer, value.Order.Value, options);
			}
			if (value.Mode.HasValue)
			{
				writer.WritePropertyName("mode");
				JsonSerializer.Serialize(writer, value.Mode.Value, options);
			}
			if (value.NumericType.HasValue)
			{
				writer.WritePropertyName("numeric_type");
				JsonSerializer.Serialize(writer, value.NumericType.Value, options);
			}
			if (value.Missing != null)
			{
				writer.WritePropertyName("missing");
				JsonSerializer.Serialize(writer, value.Missing, options);
			}
			if (value.Nested != null)
			{
				writer.WritePropertyName("nested");
				JsonSerializer.Serialize(writer, value.Nested, options);
			}

			// Write the field with its points array: "<field_name>": [lat,lon, lat,lon, ...]
			if (value.Field != null)
			{
				var fieldName = _settings.Inferrer.Field(value.Field);
				writer.WritePropertyName(fieldName);
				JsonSerializer.Serialize(writer, value.Points, options);
			}

			writer.WriteEndObject();
		}

		private ISort DeserializeGeoDistanceSort(JsonElement body, JsonSerializerOptions options)
		{
			var geoSort = new GeoDistanceSort();

			foreach (var prop in body.EnumerateObject())
			{
				switch (prop.Name)
				{
					case "distance_type":
						geoSort.DistanceType = JsonSerializer.Deserialize<GeoDistanceType?>(prop.Value.GetRawText(), options);
						break;
					case "unit":
						geoSort.Unit = JsonSerializer.Deserialize<DistanceUnit?>(prop.Value.GetRawText(), options);
						break;
					case "ignore_unmapped":
						geoSort.IgnoreUnmapped = prop.Value.GetBoolean();
						break;
					case "order":
						geoSort.Order = JsonSerializer.Deserialize<SortOrder?>(prop.Value.GetRawText(), options);
						break;
					case "mode":
						geoSort.Mode = JsonSerializer.Deserialize<SortMode?>(prop.Value.GetRawText(), options);
						break;
					case "numeric_type":
						geoSort.NumericType = JsonSerializer.Deserialize<NumericType?>(prop.Value.GetRawText(), options);
						break;
					case "missing":
						geoSort.Missing = DeserializeMissing(prop.Value);
						break;
					case "nested":
						geoSort.Nested = JsonSerializer.Deserialize<NestedSort>(prop.Value.GetRawText(), options);
						break;
					default:
						// Unknown property is the field with geo points
						if (prop.Value.ValueKind == JsonValueKind.Array)
						{
							geoSort.Field = prop.Name;
							geoSort.Points = JsonSerializer.Deserialize<IEnumerable<GeoLocation>>(prop.Value.GetRawText(), options);
						}
						break;
				}
			}

			return geoSort;
		}

		private static object DeserializeMissing(JsonElement element)
		{
			return element.ValueKind switch
			{
				JsonValueKind.String => element.GetString(),
				JsonValueKind.Number => element.TryGetInt64(out var l) ? l : element.GetDouble(),
				JsonValueKind.True => true,
				JsonValueKind.False => false,
				_ => null
			};
		}
	}
}
