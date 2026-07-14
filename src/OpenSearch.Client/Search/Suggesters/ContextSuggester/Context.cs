/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/
/*
* Modifications Copyright OpenSearch Contributors. See
* GitHub history for details.
*
*  Licensed to Elasticsearch B.V. under one or more contributor
*  license agreements. See the NOTICE file distributed with
*  this work for additional information regarding copyright
*  ownership. Elasticsearch B.V. licenses this file to you under
*  the Apache License, Version 2.0 (the "License"); you may
*  not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
* 	http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing,
*  software distributed under the License is distributed on an
*  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
*  KIND, either express or implied.  See the License for the
*  specific language governing permissions and limitations
*  under the License.
*/


using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	[JsonConverter(typeof(ContextConverter))]
	[JsonFormatter(typeof(ContextFormatter))]
	public class Context : Union<string, GeoLocation>
	{
		public Context(string category) : base(category) { }

		public Context(GeoLocation geo) : base(geo) { }

		public string Category => Item1;
		public GeoLocation Geo => Item2;

		public static implicit operator Context(string context) => new Context(context);

		public static implicit operator Context(GeoLocation context) => new Context(context);
	}

	/// <summary>
	/// (De)serializes <see cref="Context"/> (a <see cref="Union{String, GeoLocation}"/> subtype).
	/// Reads via the underlying union and wraps the resulting member in a <see cref="Context"/>;
	/// writes whichever member is set. Replaces the Utf8Json <c>ContextFormatter</c>.
	/// </summary>
	internal sealed class ContextConverter : JsonConverter<Context>
	{
		public override Context Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var union = System.Text.Json.JsonSerializer.Deserialize<Union<string, GeoLocation>>(ref reader, options);
			if (union == null)
				return null;

			return union.Tag switch
			{
				0 => new Context(union.Item1),
				1 => new Context(union.Item2),
				_ => null
			};
		}

		public override void Write(Utf8JsonWriter writer, Context value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			System.Text.Json.JsonSerializer.Serialize(writer, value, typeof(Union<string, GeoLocation>), options);
		}
	}
	internal class ContextFormatter : IJsonFormatter<Context>
	{
		public Context Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var formatter = formatterResolver.GetFormatter<Union<string, GeoLocation>>();
			var union = formatter.Deserialize(ref reader, formatterResolver);
			switch (union.Tag)
			{
				case 0:
					return new Context(union.Item1);
				case 1:
					return new Context(union.Item2);
				default:
					return null;
			}
		}

		public void Serialize(ref JsonWriter writer, Context value, IJsonFormatterResolver formatterResolver)
		{
			var formatter = formatterResolver.GetFormatter<Union<string, GeoLocation>>();
			formatter.Serialize(ref writer, value, formatterResolver);
		}
	}


}
