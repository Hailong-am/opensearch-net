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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IRankFeatureQuery"/> that handles
	/// the rank_feature query with its various score function types (saturation, log, sigmoid, linear).
	/// <para>
	/// Example JSON shape:
	/// <code>
	/// {
	///   "_name": "...",
	///   "boost": 1.0,
	///   "field": "pagerank",
	///   "saturation": { "pivot": 8 }
	/// }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class RankFeatureQueryConverter : JsonConverter<IRankFeatureQuery>
	{
		public override IRankFeatureQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var query = new RankFeatureQuery();

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
					case "_name":
						query.Name = reader.GetString();
						break;
					case "boost":
						query.Boost = reader.GetDouble();
						break;
					case "field":
						query.Field = JsonSerializer.Deserialize<Field>(ref reader, options);
						break;
					case "saturation":
						query.Function = JsonSerializer.Deserialize<RankFeatureSaturationFunction>(ref reader, options);
						break;
					case "log":
						query.Function = JsonSerializer.Deserialize<RankFeatureLogarithmFunction>(ref reader, options);
						break;
					case "sigmoid":
						query.Function = JsonSerializer.Deserialize<RankFeatureSigmoidFunction>(ref reader, options);
						break;
					case "linear":
						query.Function = JsonSerializer.Deserialize<RankFeatureLinearFunction>(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return query;
		}

		public override void Write(Utf8JsonWriter writer, IRankFeatureQuery value, JsonSerializerOptions options)
		{
			if (value == null)
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

			if (value.Field != null)
			{
				writer.WritePropertyName("field");
				JsonSerializer.Serialize(writer, value.Field, options);
			}

			if (value.Function != null)
			{
				switch (value.Function)
				{
					case IRankFeatureSigmoidFunction sigmoid:
						writer.WritePropertyName("sigmoid");
						JsonSerializer.Serialize(writer, sigmoid, options);
						break;
					case IRankFeatureSaturationFunction saturation:
						writer.WritePropertyName("saturation");
						JsonSerializer.Serialize(writer, saturation, options);
						break;
					case IRankFeatureLogarithmFunction log:
						writer.WritePropertyName("log");
						JsonSerializer.Serialize(writer, log, options);
						break;
					case IRankFeatureLinearFunction linear:
						writer.WritePropertyName("linear");
						JsonSerializer.Serialize(writer, linear, options);
						break;
				}
			}

			writer.WriteEndObject();
		}
	}
}
