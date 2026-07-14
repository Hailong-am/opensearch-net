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
	/// Serializes an <see cref="IEnumerable{Double}"/> using the options-registered
	/// <see cref="double"/> converter for each element, so integral values keep their trailing
	/// <c>.0</c> (e.g. <c>95.0</c> not <c>95</c>). System.Text.Json skips the options-registered
	/// <c>DoubleConverter</c> for collection elements at depth, so this converter is pinned onto
	/// <c>IEnumerable&lt;double&gt;</c> contract properties by <see cref="InterfaceDataContractModifier"/>.
	/// </summary>
	internal sealed class DoubleEnumerableConverter : JsonConverter<IEnumerable<double>>
	{
		public override IEnumerable<double> Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartArray)
			{
				reader.Skip();
				return null;
			}

			var doubleConverter = (JsonConverter<double>)options.GetConverter(typeof(double));
			var list = new List<double>();

			while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				list.Add(doubleConverter.Read(ref reader, typeof(double), options));

			return list;
		}

		public override void Write(Utf8JsonWriter writer, IEnumerable<double> value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var doubleConverter = (JsonConverter<double>)options.GetConverter(typeof(double));

			writer.WriteStartArray();
			foreach (var v in value)
				doubleConverter.Write(writer, v, options);
			writer.WriteEndArray();
		}
	}
}
