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
	/// Serializes an <see cref="ISourceOnlyRepository"/> as
	/// <c>{ "type": "source", "settings": { "delegate_type": &lt;type&gt;, ...delegate settings... } }</c>.
	/// Mirrors the historical (Utf8Json) <c>SourceOnlyRepositoryFormatter</c>: the delegate
	/// repository's settings are flattened alongside the <c>delegate_type</c> discriminator.
	/// </summary>
	internal sealed class SourceOnlyRepositoryConverter : JsonConverter<ISourceOnlyRepository>
	{
		public override ISourceOnlyRepository Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			if (root.ValueKind != JsonValueKind.Object
				|| !root.TryGetProperty("settings", out var settings)
				|| settings.ValueKind != JsonValueKind.Object
				|| !settings.TryGetProperty("delegate_type", out var delegateTypeElement))
				return null;

			var delegateType = delegateTypeElement.GetString();

			object delegateSettings = delegateType switch
			{
				"s3" => settings.Deserialize<S3RepositorySettings>(options),
				"azure" => settings.Deserialize<AzureRepositorySettings>(options),
				"url" => settings.Deserialize<ReadOnlyUrlRepositorySettings>(options),
				"hdfs" => settings.Deserialize<HdfsRepositorySettings>(options),
				"fs" => settings.Deserialize<FileSystemRepositorySettings>(options),
				_ => null
			};

			return new SourceOnlyRepository(delegateType, delegateSettings);
		}

		public override void Write(Utf8JsonWriter writer, ISourceOnlyRepository value, JsonSerializerOptions options)
		{
			if (value == null || string.IsNullOrEmpty(value.DelegateType))
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WriteString("type", "source");

			var delegateSettings = ((IRepositoryWithSettings)value).DelegateSettings;
			if (delegateSettings != null)
			{
				writer.WritePropertyName("settings");
				writer.WriteStartObject();
				writer.WriteString("delegate_type", value.DelegateType);

				// Serialize the delegate settings by runtime type, then splice its members
				// alongside delegate_type inside the same settings object.
				using var settingsDoc = JsonSerializer.SerializeToDocument(
					delegateSettings, delegateSettings.GetType(), options);
				if (settingsDoc.RootElement.ValueKind == JsonValueKind.Object)
				{
					foreach (var prop in settingsDoc.RootElement.EnumerateObject())
						prop.WriteTo(writer);
				}

				writer.WriteEndObject();
			}

			writer.WriteEndObject();
		}
	}
}
