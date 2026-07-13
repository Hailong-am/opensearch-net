/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Runtime.Serialization;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Client
{
	internal class RankFeatureQueryFormatter : IJsonFormatter<IRankFeatureQuery>
	{
		public void Serialize(ref JsonWriter writer, IRankFeatureQuery value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteBeginObject();

			if (!string.IsNullOrEmpty(value.Name))
			{
				writer.WritePropertyName("_name");
				writer.WriteString(value.Name);
				writer.WriteValueSeparator();
			}

			if (value.Boost.HasValue)
			{
				writer.WritePropertyName("boost");
				writer.WriteDouble(value.Boost.Value);
				writer.WriteValueSeparator();
			}

			writer.WritePropertyName("field");
			var fieldFormatter = formatterResolver.GetFormatter<Field>();
			fieldFormatter.Serialize(ref writer, value.Field, formatterResolver);

			if (value.Function != null)
			{
				writer.WriteValueSeparator();
				switch (value.Function)
				{
					case IRankFeatureSigmoidFunction sigmoid:
						SerializeScoreFunction(ref writer, "sigmoid", sigmoid, formatterResolver);
						break;
					case IRankFeatureSaturationFunction saturation:
						SerializeScoreFunction(ref writer, "saturation", saturation, formatterResolver);
						break;
					case IRankFeatureLogarithmFunction log:
						SerializeScoreFunction(ref writer, "log", log, formatterResolver);
						break;
					case IRankFeatureLinearFunction log:
						SerializeScoreFunction(ref writer, "linear", log, formatterResolver);
						break;
				}
			}

			writer.WriteEndObject();
		}

		private static void SerializeScoreFunction<TScoreFunction>(ref JsonWriter writer, string name, TScoreFunction scoreFunction,
			IJsonFormatterResolver formatterResolver
		) where TScoreFunction : IRankFeatureFunction
		{
			writer.WritePropertyName(name);
			formatterResolver.GetFormatter<TScoreFunction>()
				.Serialize(ref writer, scoreFunction, formatterResolver);
		}

		private static IRankFeatureFunction DeserializeScoreFunction<TScoreFunction>(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
			where TScoreFunction : IRankFeatureFunction =>
			formatterResolver.GetFormatter<TScoreFunction>().Deserialize(ref reader, formatterResolver);

		private static readonly AutomataDictionary Fields = new AutomataDictionary
		{
			{ "_name", 0 },
			{ "boost", 1 },
			{ "field", 2 },
			{ "saturation", 3 },
			{ "log", 4 },
			{ "sigmoid", 5 },
			{ "linear", 6 }
		};

		public IRankFeatureQuery Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.ReadIsNull())
				return null;

			var query = new RankFeatureQuery();
			var count = 0;
			while (reader.ReadIsInObject(ref count))
			{
				if (Fields.TryGetValue(reader.ReadPropertyNameSegmentRaw(), out var value))
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
							query.Field = formatterResolver.GetFormatter<Field>().Deserialize(ref reader, formatterResolver);
							break;
						case 3:
							query.Function = DeserializeScoreFunction<RankFeatureSaturationFunction>(ref reader, formatterResolver);
							break;
						case 4:
							query.Function = DeserializeScoreFunction<RankFeatureLogarithmFunction>(ref reader, formatterResolver);
							break;
						case 5:
							query.Function = DeserializeScoreFunction<RankFeatureSigmoidFunction>(ref reader, formatterResolver);
							break;
						case 6:
							query.Function = DeserializeScoreFunction<RankFeatureLinearFunction>(ref reader, formatterResolver);
							break;
					}
				}
				else
					reader.ReadNextBlock();
			}

			return query;
		}
	}

}
