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
	internal sealed class JoinFieldConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public JoinFieldConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => typeToConvert == typeof(JoinField);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			new JoinFieldConverter(_settings);
	}

	internal sealed class JoinFieldConverter : JsonConverter<JoinField>
	{
		private readonly IConnectionSettingsValues _settings;

		public JoinFieldConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override JoinField Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType == JsonTokenType.String)
			{
				var parent = reader.GetString();
				return new JoinField(new JoinField.Parent(parent));
			}

			Id parentId = null;
			string name = null;
			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var propertyName = reader.GetString();
				reader.Read();
				switch (propertyName)
				{
					case "parent":
						parentId = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
						break;
					case "name":
						name = reader.TokenType == JsonTokenType.Null ? null : reader.GetString();
						break;
				}
			}

			return parentId != null
				? new JoinField(new JoinField.Child(name, parentId))
				: new JoinField(new JoinField.Parent(name));
		}

		public override void Write(Utf8JsonWriter writer, JoinField value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			switch (value.Tag)
			{
				case 0:
				{
					var relationName = _settings.Inferrer.RelationName(value.ParentOption.Name);
					writer.WriteStringValue(relationName);
					break;
				}
				case 1:
				{
					var child = value.ChildOption;
					writer.WriteStartObject();
					writer.WritePropertyName("name");
					writer.WriteStringValue(_settings.Inferrer.RelationName(child.Name));
					writer.WritePropertyName("parent");
					var id = (child.ParentId as IUrlParameter)?.GetString(_settings);
					writer.WriteStringValue(id);
					writer.WriteEndObject();
					break;
				}
			}
		}
	}
}
