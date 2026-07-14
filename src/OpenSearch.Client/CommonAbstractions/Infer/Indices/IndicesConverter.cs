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
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes <see cref="Indices"/> using the "multi syntax" wire format: a single
	/// comma-separated string (e.g. <c>"index-a,index-b"</c>) or <c>"_all"</c>. This matches
	/// the historical (Utf8Json) default (<c>IndicesMultiSyntaxFormatter</c>) applied at the
	/// <see cref="Indices"/> type level. Reading also accepts a JSON array of index strings.
	/// </summary>
	internal sealed class IndicesConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public IndicesConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Indices);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new IndicesConverter(_settings);
	}

	internal sealed class IndicesConverter : JsonConverter<Indices>
	{
		private readonly IConnectionSettingsValues _settings;

		public IndicesConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override Indices Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.String:
					return Indices.Parse(reader.GetString());
				case JsonTokenType.StartArray:
				{
					var indices = new List<IndexName>();
					while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
					{
						var s = reader.GetString();
						if (s != null)
							indices.Add(s);
					}
					return indices.Count == 0 ? null : Indices.Index(indices);
				}
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, Indices value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStringValue(((IUrlParameter)value).GetString(_settings));
		}
	}
}
