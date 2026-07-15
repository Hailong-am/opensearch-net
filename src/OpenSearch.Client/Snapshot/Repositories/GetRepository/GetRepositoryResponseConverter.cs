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
	/// Deserializes a <see cref="GetRepositoryResponse"/> whose root object is a map of
	/// <c>{ "&lt;repository-name&gt;": { "type": "...", "settings": { ... } } }</c>.
	/// Dispatches each repository on its <c>type</c> discriminator (fs/url/azure/s3/hdfs/source) and
	/// tolerates a missing <c>settings</c> object. Also honours the top-level <c>error</c>/<c>status</c>
	/// server-error fields. Mirrors the historical (Utf8Json) <c>GetRepositoryResponseFormatter</c>.
	/// </summary>
	internal sealed class GetRepositoryResponseConverter : JsonConverter<GetRepositoryResponse>
	{
		public override GetRepositoryResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var response = new GetRepositoryResponse();

			if (reader.TokenType == JsonTokenType.Null)
				return response;

			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Expected StartObject, got {reader.TokenType}");

			var repositories = new Dictionary<string, ISnapshotRepository>();

			reader.Read();
			while (reader.TokenType != JsonTokenType.EndObject)
			{
				var name = reader.GetString();
				reader.Read();

				if (ResponseFormatterHelpers.ServerErrorFields.TryGetValue(name, out var errorValue))
				{
					switch (errorValue)
					{
						case 0: // "error"
							if (reader.TokenType == JsonTokenType.String)
								response.Error = new Error { Reason = reader.GetString() };
							else
								response.Error = JsonSerializer.Deserialize<Error>(ref reader, options);
							break;
						case 1: // "status"
							response.StatusCode = reader.TokenType == JsonTokenType.Number ? reader.GetInt32() : (int?)null;
							if (reader.TokenType != JsonTokenType.Number)
								reader.Skip();
							break;
					}

					reader.Read();
					continue;
				}

				using var doc = JsonDocument.ParseValue(ref reader);
				var root = doc.RootElement;

				var repository = ReadRepository(root, options);
				if (repository != null)
					repositories.Add(name, repository);

				reader.Read();
			}

			response.Repositories = repositories;
			return response;
		}

		private static ISnapshotRepository ReadRepository(JsonElement root, JsonSerializerOptions options)
		{
			if (root.ValueKind != JsonValueKind.Object || !root.TryGetProperty("type", out var typeElement))
				return null;

			var type = typeElement.GetString();
			var hasSettings = root.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object;

			switch (type)
			{
				case "fs":
					return new FileSystemRepository(hasSettings ? settings.Deserialize<FileSystemRepositorySettings>(options) : null);
				case "url":
					return new ReadOnlyUrlRepository(hasSettings ? settings.Deserialize<ReadOnlyUrlRepositorySettings>(options) : null);
				case "azure":
					return new AzureRepository(hasSettings ? settings.Deserialize<AzureRepositorySettings>(options) : null);
				case "s3":
					return new S3Repository(hasSettings ? settings.Deserialize<S3RepositorySettings>(options) : null);
				case "hdfs":
					return new HdfsRepository(hasSettings ? settings.Deserialize<HdfsRepositorySettings>(options) : null);
				case "source":
					return root.Deserialize<ISourceOnlyRepository>(options);
				default:
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, GetRepositoryResponse value, JsonSerializerOptions options)
		{
			if (value?.Repositories == null)
			{
				writer.WriteStartObject();
				writer.WriteEndObject();
				return;
			}

			writer.WriteStartObject();
			foreach (var kvp in value.Repositories)
			{
				writer.WritePropertyName(kvp.Key);
				if (kvp.Value == null)
					writer.WriteNullValue();
				else
					JsonSerializer.Serialize(writer, kvp.Value, kvp.Value.GetType(), options);
			}
			writer.WriteEndObject();
		}
	}
}
