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
	/// Serializes the <c>moving_avg</c> pipeline aggregation. The polymorphic
	/// <see cref="IMovingAverageModel"/> is written as a <c>model</c> name string plus a
	/// <c>settings</c> object holding the model's parameters. Replaces the Utf8Json
	/// <c>MovingAverageAggregationFormatter</c>.
	/// </summary>
	internal sealed class MovingAverageAggregationConverter : JsonConverter<IMovingAverageAggregation>
	{
		public override IMovingAverageAggregation Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var agg = new MovingAverageAggregation();
			string modelName = null;
			JsonElement settings = default;
			var hasSettings = false;

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var propertyName = reader.GetString();
				reader.Read();

				switch (propertyName)
				{
					case "buckets_path":
						agg.BucketsPath = JsonSerializer.Deserialize<IBucketsPath>(ref reader, options);
						break;
					case "format":
						agg.Format = reader.GetString();
						break;
					case "gap_policy":
						agg.GapPolicy = JsonSerializer.Deserialize<GapPolicy?>(ref reader, options);
						break;
					case "minimize":
						agg.Minimize = reader.GetBoolean();
						break;
					case "predict":
						agg.Predict = reader.GetInt32();
						break;
					case "window":
						agg.Window = reader.GetInt32();
						break;
					case "model":
						modelName = reader.GetString();
						break;
					case "settings":
						settings = JsonElement.ParseValue(ref reader);
						hasSettings = true;
						break;
					default:
						reader.Skip();
						break;
				}
			}

			if (modelName != null)
			{
				var json = hasSettings ? settings.GetRawText() : "{}";
				agg.Model = modelName switch
				{
					"linear" => JsonSerializer.Deserialize<LinearModel>(json, options),
					"simple" => JsonSerializer.Deserialize<SimpleModel>(json, options),
					"ewma" => JsonSerializer.Deserialize<EwmaModel>(json, options),
					"holt" => JsonSerializer.Deserialize<HoltLinearModel>(json, options),
					"holt_winters" => JsonSerializer.Deserialize<HoltWintersModel>(json, options),
					_ => null
				};
			}

			return agg;
		}

		public override void Write(Utf8JsonWriter writer, IMovingAverageAggregation value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (value.BucketsPath != null)
			{
				writer.WritePropertyName("buckets_path");
				JsonSerializer.Serialize(writer, value.BucketsPath, options);
			}

			if (value.GapPolicy != null)
			{
				writer.WritePropertyName("gap_policy");
				JsonSerializer.Serialize(writer, value.GapPolicy, options);
			}

			if (!string.IsNullOrEmpty(value.Format))
				writer.WriteString("format", value.Format);

			if (value.Window != null)
				writer.WriteNumber("window", value.Window.Value);

			if (value.Minimize != null)
				writer.WriteBoolean("minimize", value.Minimize.Value);

			if (value.Predict != null)
				writer.WriteNumber("predict", value.Predict.Value);

			if (value.Model != null)
			{
				writer.WriteString("model", value.Model.Name);
				writer.WritePropertyName("settings");
				JsonSerializer.Serialize(writer, value.Model, value.Model.GetType(), options);
			}

			writer.WriteEndObject();
		}
	}
}
