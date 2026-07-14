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
using OpenSearch.Net;

namespace OpenSearch.Client
{
	public interface ISortOrder
	{
		string Key { get; set; }

		SortOrder Order { get; set; }
	}

	/// <summary>
	/// Serializes an <see cref="ISortOrder"/> as a single-key object <c>{ "&lt;key&gt;": &lt;order&gt; }</c>.
	/// Replaces the Utf8Json <c>SortOrderFormatter</c>. Declared via <c>[JsonConverter]</c> on the concrete
	/// order types so it is honored in every context (including list elements).
	/// </summary>
	internal sealed class SortOrderConverter<TSortOrder> : JsonConverter<TSortOrder>
		where TSortOrder : class, ISortOrder, new()
	{
		public override TSortOrder Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var sortOrder = new TSortOrder();
			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;
				sortOrder.Key = reader.GetString();
				reader.Read();
				sortOrder.Order = System.Text.Json.JsonSerializer.Deserialize<SortOrder>(ref reader, options);
			}

			return sortOrder;
		}

		public override void Write(Utf8JsonWriter writer, TSortOrder value, JsonSerializerOptions options)
		{
			if (value?.Key == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(value.Key);
			System.Text.Json.JsonSerializer.Serialize(writer, value.Order, options);
			writer.WriteEndObject();
		}
	}

	[JsonConverter(typeof(SortOrderConverter<HistogramOrder>))]
	[JsonFormatter(typeof(SortOrderFormatter<HistogramOrder>))]
	public class HistogramOrder : ISortOrder
	{
		public static HistogramOrder CountAscending => new HistogramOrder { Key = "_count", Order = SortOrder.Ascending };
		public static HistogramOrder CountDescending => new HistogramOrder { Key = "_count", Order = SortOrder.Descending };
		public string Key { get; set; }

		public static HistogramOrder KeyAscending => new HistogramOrder { Key = "_key", Order = SortOrder.Ascending };
		public static HistogramOrder KeyDescending => new HistogramOrder { Key = "_key", Order = SortOrder.Descending };
		public SortOrder Order { get; set; }
	}
}
