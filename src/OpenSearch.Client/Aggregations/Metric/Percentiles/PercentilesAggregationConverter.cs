/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// Shared serialization helpers for the <c>percentiles</c> and <c>percentile_ranks</c>
	/// aggregations, which both wrap the polymorphic <see cref="IPercentilesMethod"/> under
	/// a <c>hdr</c>/<c>tdigest</c> key and share the metric-aggregation body.
	/// </summary>
	internal static class PercentilesMethodJson
	{
		public static void WriteMethod(Utf8JsonWriter writer, IPercentilesMethod method, JsonSerializerOptions options)
		{
			switch (method)
			{
				case ITDigestMethod tdigest:
					writer.WritePropertyName("tdigest");
					writer.WriteStartObject();
					if (tdigest.Compression.HasValue)
					{
						writer.WritePropertyName("compression");
						var doubleConverter = (JsonConverter<double>)options.GetConverter(typeof(double));
						doubleConverter.Write(writer, tdigest.Compression.Value, options);
					}
					writer.WriteEndObject();
					break;
				case IHDRHistogramMethod hdr:
					writer.WritePropertyName("hdr");
					writer.WriteStartObject();
					if (hdr.NumberOfSignificantValueDigits.HasValue)
						writer.WriteNumber("number_of_significant_value_digits", hdr.NumberOfSignificantValueDigits.Value);
					writer.WriteEndObject();
					break;
			}
		}

		public static void WriteDoubleArray(Utf8JsonWriter writer, IEnumerable<double> values, JsonSerializerOptions options)
		{
			var doubleConverter = (JsonConverter<double>)options.GetConverter(typeof(double));
			writer.WriteStartArray();
			foreach (var v in values)
				doubleConverter.Write(writer, v, options);
			writer.WriteEndArray();
		}

		public static void WriteMetricBody(
			Utf8JsonWriter writer,
			IMetricAggregation value,
			JsonSerializerOptions options)
		{
			if (value.Meta != null && value.Meta.Any())
			{
				writer.WritePropertyName("meta");
				JsonSerializer.Serialize(writer, value.Meta, options);
			}

			if (value.Field != null)
			{
				writer.WritePropertyName("field");
				JsonSerializer.Serialize(writer, value.Field, options);
			}

			if (value.Script != null)
			{
				writer.WritePropertyName("script");
				JsonSerializer.Serialize(writer, value.Script, options);
			}

			if (value.Missing.HasValue)
			{
				writer.WritePropertyName("missing");
				var doubleConverter = (JsonConverter<double>)options.GetConverter(typeof(double));
				doubleConverter.Write(writer, value.Missing.Value, options);
			}
		}
	}

	internal sealed class PercentilesAggregationConverter : JsonConverter<IPercentilesAggregation>
	{
		public override IPercentilesAggregation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var agg = new PercentilesAggregation();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var propertyName = reader.GetString();
				reader.Read();

				switch (propertyName)
				{
					case "hdr":
						agg.Method = JsonSerializer.Deserialize<HDRHistogramMethod>(ref reader, options);
						break;
					case "tdigest":
						agg.Method = JsonSerializer.Deserialize<TDigestMethod>(ref reader, options);
						break;
					case "field":
						agg.Field = JsonSerializer.Deserialize<Field>(ref reader, options);
						break;
					case "script":
						agg.Script = JsonSerializer.Deserialize<IScript>(ref reader, options);
						break;
					case "missing":
						agg.Missing = reader.GetDouble();
						break;
					case "percents":
						agg.Percents = JsonSerializer.Deserialize<IEnumerable<double>>(ref reader, options);
						break;
					case "meta":
						agg.Meta = JsonSerializer.Deserialize<IDictionary<string, object>>(ref reader, options);
						break;
					case "keyed":
						agg.Keyed = reader.GetBoolean();
						break;
					case "format":
						agg.Format = reader.GetString();
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return agg;
		}

		public override void Write(Utf8JsonWriter writer, IPercentilesAggregation value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			PercentilesMethodJson.WriteMetricBody(writer, value, options);

			if (value.Method != null)
				PercentilesMethodJson.WriteMethod(writer, value.Method, options);

			if (value.Percents != null)
			{
				writer.WritePropertyName("percents");
				PercentilesMethodJson.WriteDoubleArray(writer, value.Percents, options);
			}

			if (value.Keyed.HasValue)
				writer.WriteBoolean("keyed", value.Keyed.Value);

			if (!string.IsNullOrEmpty(value.Format))
				writer.WriteString("format", value.Format);

			writer.WriteEndObject();
		}
	}

	internal sealed class PercentileRanksAggregationConverter : JsonConverter<IPercentileRanksAggregation>
	{
		public override IPercentileRanksAggregation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var agg = new PercentileRanksAggregation();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var propertyName = reader.GetString();
				reader.Read();

				switch (propertyName)
				{
					case "hdr":
						agg.Method = JsonSerializer.Deserialize<HDRHistogramMethod>(ref reader, options);
						break;
					case "tdigest":
						agg.Method = JsonSerializer.Deserialize<TDigestMethod>(ref reader, options);
						break;
					case "field":
						agg.Field = JsonSerializer.Deserialize<Field>(ref reader, options);
						break;
					case "script":
						agg.Script = JsonSerializer.Deserialize<IScript>(ref reader, options);
						break;
					case "missing":
						agg.Missing = reader.GetDouble();
						break;
					case "values":
						agg.Values = JsonSerializer.Deserialize<IEnumerable<double>>(ref reader, options);
						break;
					case "meta":
						agg.Meta = JsonSerializer.Deserialize<IDictionary<string, object>>(ref reader, options);
						break;
					case "keyed":
						agg.Keyed = reader.GetBoolean();
						break;
					case "format":
						agg.Format = reader.GetString();
						break;
					default:
						reader.Skip();
						break;
				}
			}

			return agg;
		}

		public override void Write(Utf8JsonWriter writer, IPercentileRanksAggregation value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			PercentilesMethodJson.WriteMetricBody(writer, value, options);

			if (value.Method != null)
				PercentilesMethodJson.WriteMethod(writer, value.Method, options);

			if (value.Values != null && value.Values.Any())
			{
				writer.WritePropertyName("values");
				PercentilesMethodJson.WriteDoubleArray(writer, value.Values, options);
			}

			if (value.Keyed.HasValue)
				writer.WriteBoolean("keyed", value.Keyed.Value);

			if (!string.IsNullOrEmpty(value.Format))
				writer.WriteString("format", value.Format);

			writer.WriteEndObject();
		}
	}
}
