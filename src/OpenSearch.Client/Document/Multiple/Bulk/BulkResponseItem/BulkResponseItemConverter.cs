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
	/// Deserializes a bulk response item, which is a single-key object whose key is the
	/// operation (<c>index</c>/<c>create</c>/<c>update</c>/<c>delete</c>) and whose value is the
	/// concrete item body. Replaces the Utf8Json <c>BulkResponseItemFormatter</c>.
	/// </summary>
	internal sealed class BulkResponseItemConverter : JsonConverter<BulkResponseItemBase>
	{
		public override BulkResponseItemBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			reader.Read(); // move to property name
			if (reader.TokenType != JsonTokenType.PropertyName)
			{
				// empty object
				return null;
			}

			var operation = reader.GetString();
			reader.Read(); // move to the item body

			BulkResponseItemBase item;
			switch (operation)
			{
				case "delete":
					item = JsonSerializer.Deserialize<BulkDeleteResponseItem>(ref reader, options);
					break;
				case "update":
					item = JsonSerializer.Deserialize<BulkUpdateResponseItem>(ref reader, options);
					break;
				case "index":
					item = JsonSerializer.Deserialize<BulkIndexResponseItem>(ref reader, options);
					break;
				case "create":
					item = JsonSerializer.Deserialize<BulkCreateResponseItem>(ref reader, options);
					break;
				default:
					reader.Skip();
					item = null;
					break;
			}

			reader.Read(); // consume EndObject of the wrapper
			return item;
		}

		public override void Write(Utf8JsonWriter writer, BulkResponseItemBase value, JsonSerializerOptions options) =>
			throw new NotSupportedException("Bulk response items are only ever deserialized.");
	}
}
