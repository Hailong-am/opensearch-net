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

using JsonFormatterAttribute = OpenSearch.Net.Utf8Json.JsonFormatterAttribute;
namespace OpenSearch.Client
{
	[JsonConverter(typeof(ReindexRoutingConverter))]
	[JsonFormatter(typeof(ReindexRoutingFormatter))]
	public class ReindexRouting
	{
		public static ReindexRouting Discard = new ReindexRouting("discard", true);
		public static ReindexRouting Keep = new ReindexRouting("keep", true);
		private readonly string _newRoutingValue;

		/// <summary>
		/// Use ReindexRouting.Keep or ReindexRouting.Discard if you want to sent "keep" or "discard", this
		/// constructor always sends newRoutingValue prefixed with '='
		/// </summary>
		public ReindexRouting(string newRoutingValue) : this(newRoutingValue, false) { }

		private ReindexRouting(string newRoutingValue, bool noPrefix)
		{
			var routing = newRoutingValue.TrimStart('=');
			var prefix = noPrefix ? "" : "=";
			_newRoutingValue = $"{prefix}{routing}";
		}

		public static implicit operator ReindexRouting(string routing) => new ReindexRouting(routing);

		public override string ToString() => _newRoutingValue;
	}

	internal sealed class ReindexRoutingConverter : JsonConverter<ReindexRouting>
	{
		public override ReindexRouting Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var value = reader.GetString();
			switch (value)
			{
				case "keep": return ReindexRouting.Keep;
				case "discard": return ReindexRouting.Discard;
				default: return value == null ? null : new ReindexRouting(value);
			}
		}

		public override void Write(Utf8JsonWriter writer, ReindexRouting value, JsonSerializerOptions options)
		{
			if (value == null)
				writer.WriteNullValue();
			else
				writer.WriteStringValue(value.ToString());
		}
	}
}
