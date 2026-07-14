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
	/// A <see cref="JsonConverter{T}"/> for <see cref="IScoreFunction"/> that handles the
	/// polymorphic function_score function shape (decay: exp/gauss/linear, field_value_factor,
	/// random_score, script_score, weight) plus the shared <c>filter</c>/<c>weight</c> members.
	/// <para>
	/// This is the System.Text.Json equivalent of the Utf8Json <c>ScoreFunctionJsonFormatter</c>.
	/// </para>
	/// </summary>
	internal sealed class ScoreFunctionConverter : JsonConverter<IScoreFunction>
	{
		private readonly IConnectionSettingsValues _settings;

		public ScoreFunctionConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override IScoreFunction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			QueryContainer filter = null;
			double? weight = null;
			IScoreFunction function = null;

			foreach (var prop in root.EnumerateObject())
			{
				switch (prop.Name)
				{
					case "filter":
						filter = prop.Value.Deserialize<QueryContainer>(options);
						break;
					case "weight":
						weight = prop.Value.GetDouble();
						break;
					case "exp":
					case "gauss":
					case "linear":
						function = ReadDecay(prop.Name, prop.Value, options);
						break;
					case "random_score":
						function = prop.Value.Deserialize<RandomScoreFunction>(options);
						break;
					case "field_value_factor":
						function = prop.Value.Deserialize<FieldValueFactorFunction>(options);
						break;
					case "script_score":
						function = prop.Value.Deserialize<ScriptScoreFunction>(options);
						break;
				}
			}

			if (function == null)
			{
				if (weight.HasValue)
					function = new WeightFunction();
				else
					return null;
			}

			function.Weight = weight;
			function.Filter = filter;
			return function;
		}

		private static IScoreFunction ReadDecay(string type, JsonElement body, JsonSerializerOptions options)
		{
			MultiValueMode? multiValueMode = null;
			JsonElement fieldBody = default;
			string fieldName = null;

			foreach (var prop in body.EnumerateObject())
			{
				if (prop.Name == "multi_value_mode")
					multiValueMode = prop.Value.Deserialize<MultiValueMode>(options);
				else
				{
					fieldName = prop.Name;
					fieldBody = prop.Value;
				}
			}

			if (fieldName == null)
				return null;

			// Determine the origin/scale sub-type from the "origin" value shape.
			var subType = "numeric";
			if (fieldBody.ValueKind == JsonValueKind.Object && fieldBody.TryGetProperty("origin", out var origin))
			{
				subType = origin.ValueKind switch
				{
					JsonValueKind.String => "date",
					JsonValueKind.Object => "geo",
					_ => "numeric"
				};
			}

			var raw = fieldBody.GetRawText();
			IDecayFunction decay = type switch
			{
				"exp" => subType switch
				{
					"date" => JsonSerializer.Deserialize<ExponentialDateDecayFunction>(raw, options),
					"geo" => JsonSerializer.Deserialize<ExponentialGeoDecayFunction>(raw, options),
					_ => JsonSerializer.Deserialize<ExponentialDecayFunction>(raw, options)
				},
				"gauss" => subType switch
				{
					"date" => JsonSerializer.Deserialize<GaussDateDecayFunction>(raw, options),
					"geo" => JsonSerializer.Deserialize<GaussGeoDecayFunction>(raw, options),
					_ => JsonSerializer.Deserialize<GaussDecayFunction>(raw, options)
				},
				"linear" => subType switch
				{
					"date" => JsonSerializer.Deserialize<LinearDateDecayFunction>(raw, options),
					"geo" => JsonSerializer.Deserialize<LinearGeoDecayFunction>(raw, options),
					_ => JsonSerializer.Deserialize<LinearDecayFunction>(raw, options)
				},
				_ => null
			};

			if (decay != null)
			{
				decay.Field = fieldName;
				decay.MultiValueMode = multiValueMode;
			}

			return decay;
		}

		public override void Write(Utf8JsonWriter writer, IScoreFunction value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (value.Filter != null)
			{
				writer.WritePropertyName("filter");
				JsonSerializer.Serialize(writer, value.Filter, options);
			}

			switch (value)
			{
				case IDecayFunction decayFunction:
					WriteDecay(writer, decayFunction, options);
					break;
				case IFieldValueFactorFunction fieldValueFactorFunction:
					WriteFieldValueFactor(writer, fieldValueFactorFunction, options);
					break;
				case IRandomScoreFunction randomScoreFunction:
					WriteRandomScore(writer, randomScoreFunction, options);
					break;
				case IScriptScoreFunction scriptScoreFunction:
					WriteScriptScore(writer, scriptScoreFunction, options);
					break;
				case IWeightFunction _:
					break;
				default:
					throw new JsonException($"Can not write function score json for {value.GetType().Name}");
			}

			if (value.Weight.HasValue)
			{
				writer.WritePropertyName("weight");
				JsonSerializer.Serialize(writer, value.Weight.Value, options);
			}

			writer.WriteEndObject();
		}

		private void WriteScriptScore(Utf8JsonWriter writer, IScriptScoreFunction value, JsonSerializerOptions options)
		{
			writer.WritePropertyName("script_score");
			writer.WriteStartObject();
			writer.WritePropertyName("script");
			JsonSerializer.Serialize(writer, value?.Script, options);
			writer.WriteEndObject();
		}

		private void WriteRandomScore(Utf8JsonWriter writer, IRandomScoreFunction value, JsonSerializerOptions options)
		{
			writer.WritePropertyName("random_score");
			writer.WriteStartObject();

			if (value.Seed != null)
			{
				writer.WritePropertyName("seed");
				JsonSerializer.Serialize(writer, value.Seed, options);
			}

			if (value.Field != null)
			{
				writer.WritePropertyName("field");
				JsonSerializer.Serialize(writer, value.Field, options);
			}

			writer.WriteEndObject();
		}

		private void WriteFieldValueFactor(Utf8JsonWriter writer, IFieldValueFactorFunction value, JsonSerializerOptions options)
		{
			writer.WritePropertyName("field_value_factor");
			writer.WriteStartObject();

			writer.WritePropertyName("field");
			writer.WriteStringValue(_settings.Inferrer.Field(value.Field));

			if (value.Factor.HasValue)
			{
				writer.WritePropertyName("factor");
				JsonSerializer.Serialize(writer, value.Factor.Value, options);
			}

			if (value.Modifier.HasValue)
			{
				writer.WritePropertyName("modifier");
				JsonSerializer.Serialize(writer, value.Modifier.Value, options);
			}

			if (value.Missing.HasValue)
			{
				writer.WritePropertyName("missing");
				JsonSerializer.Serialize(writer, value.Missing.Value, options);
			}

			writer.WriteEndObject();
		}

		private void WriteDecay(Utf8JsonWriter writer, IDecayFunction decay, JsonSerializerOptions options)
		{
			writer.WritePropertyName(decay.DecayType);
			writer.WriteStartObject();

			writer.WritePropertyName(_settings.Inferrer.Field(decay.Field));
			writer.WriteStartObject();

			switch (decay)
			{
				case IDecayFunction<double?, double?> numericDecay:
					WriteNumericDecay(writer, numericDecay, options);
					break;
				case IDecayFunction<DateMath, Time> dateDecay:
					WriteDateDecay(writer, dateDecay, options);
					break;
				case IDecayFunction<GeoLocation, Distance> geoDecay:
					WriteGeoDecay(writer, geoDecay, options);
					break;
				default:
					throw new JsonException($"Can not write decay function json for {decay.GetType().Name}");
			}

			if (decay.Decay.HasValue)
			{
				writer.WritePropertyName("decay");
				JsonSerializer.Serialize(writer, decay.Decay.Value, options);
			}

			writer.WriteEndObject(); // end field body

			if (decay.MultiValueMode.HasValue)
			{
				writer.WritePropertyName("multi_value_mode");
				JsonSerializer.Serialize(writer, decay.MultiValueMode.Value, options);
			}

			writer.WriteEndObject(); // end decay type object
		}

		private static void WriteNumericDecay(Utf8JsonWriter writer, IDecayFunction<double?, double?> value, JsonSerializerOptions options)
		{
			if (value.Origin.HasValue)
			{
				writer.WritePropertyName("origin");
				JsonSerializer.Serialize(writer, value.Origin.Value, options);
			}

			if (value.Scale.HasValue)
			{
				writer.WritePropertyName("scale");
				JsonSerializer.Serialize(writer, value.Scale.Value, options);
			}

			if (value.Offset != null)
			{
				writer.WritePropertyName("offset");
				JsonSerializer.Serialize(writer, value.Offset.Value, options);
			}
		}

		private static void WriteDateDecay(Utf8JsonWriter writer, IDecayFunction<DateMath, Time> value, JsonSerializerOptions options)
		{
			if (value == null || value.Field.IsConditionless())
				return;

			if (value.Origin != null)
			{
				writer.WritePropertyName("origin");
				JsonSerializer.Serialize(writer, value.Origin, options);
			}

			writer.WritePropertyName("scale");
			JsonSerializer.Serialize(writer, value.Scale, options);

			if (value.Offset != null)
			{
				writer.WritePropertyName("offset");
				JsonSerializer.Serialize(writer, value.Offset, options);
			}
		}

		private static void WriteGeoDecay(Utf8JsonWriter writer, IDecayFunction<GeoLocation, Distance> value, JsonSerializerOptions options)
		{
			if (value == null || value.Field.IsConditionless())
				return;

			writer.WritePropertyName("origin");
			JsonSerializer.Serialize(writer, value.Origin, options);

			writer.WritePropertyName("scale");
			JsonSerializer.Serialize(writer, value.Scale, options);

			if (value.Offset != null)
			{
				writer.WritePropertyName("offset");
				JsonSerializer.Serialize(writer, value.Offset, options);
			}
		}
	}
}
