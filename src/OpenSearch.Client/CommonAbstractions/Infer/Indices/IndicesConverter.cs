/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	internal sealed class IndicesConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public IndicesConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(Indices);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new IndicesConverter(_settings);
	}

	/// <summary>
	/// Serializes <see cref="Indices"/> as its multi-index string form (<c>_all</c> or the
	/// comma-joined resolved index names) rather than the underlying <c>Union</c> object shape.
	/// Ports the Utf8Json <c>IndicesMultiSyntaxFormatter</c>.
	/// </summary>
	internal sealed class IndicesConverter : JsonConverter<Indices>
	{
		private readonly IConnectionSettingsValues _settings;

		public IndicesConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override Indices Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.String)
			{
				var value = reader.GetString();
				return value == null ? null : (Indices)value;
			}

			reader.Skip();
			return null;
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
