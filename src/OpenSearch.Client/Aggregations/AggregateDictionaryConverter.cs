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

namespace OpenSearch.Client
{
	/// <summary>
	/// STJ converter for <see cref="AggregateDictionary"/> that handles the dynamic keying
	/// pattern where aggregation names in the response map to aggregation result types.
	/// Replaces the Utf8Json-based <c>AggregateDictionaryFormatter</c>.
	/// </summary>
	/// <remarks>
	/// Aggregation responses use user-defined property names as keys. Each value is
	/// an aggregate object that is parsed heuristically by <see cref="AggregateConverter"/>.
	/// When typed_keys is enabled, the property name format is "type#name" which is split
	/// to determine the concrete type.
	/// </remarks>
	internal sealed class AggregateDictionaryResponseConverter : JsonConverter<AggregateDictionary>
	{
		private static readonly AggregateConverter Converter = new();

		public override AggregateDictionary Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var dictionary = new Dictionary<string, IAggregate>();

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return new AggregateDictionary(dictionary);
			}

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var typedProperty = reader.GetString();
				reader.Read(); // Move to value

				if (string.IsNullOrEmpty(typedProperty))
				{
					reader.Skip();
					continue;
				}

				var tokens = AggregateDictionary.TypedKeyTokens(typedProperty);
				if (tokens.Length == 1)
				{
					ParseAggregate(ref reader, options, tokens[0], dictionary);
				}
				else
				{
					ReadTypedAggregate(ref reader, options, tokens, dictionary);
				}
			}

			return new AggregateDictionary(dictionary);
		}

		public override void Write(Utf8JsonWriter writer, AggregateDictionary value, JsonSerializerOptions options) =>
			throw new NotSupportedException();

		private static void ReadTypedAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options,
			string[] tokens, Dictionary<string, IAggregate> dictionary)
		{
			var name = tokens[1];
			var type = tokens[0];

			switch (type)
			{
				case "geo_centroid":
					var geoCentroid = JsonSerializer.Deserialize<GeoCentroidAggregate>(ref reader, options);
					dictionary.Add(name, geoCentroid);
					break;
				case "geo_line":
					var geoLine = JsonSerializer.Deserialize<GeoLineAggregate>(ref reader, options);
					dictionary.Add(name, geoLine);
					break;
				default:
					// Fall back to heuristic-based parsing
					ParseAggregate(ref reader, options, name, dictionary);
					break;
			}
		}

		private static void ParseAggregate(ref Utf8JsonReader reader, JsonSerializerOptions options,
			string name, Dictionary<string, IAggregate> dictionary)
		{
			var aggregate = Converter.ReadAggregate(ref reader, options);
			dictionary.Add(name, aggregate);
		}
	}
}
