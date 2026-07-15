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
	/// Dispatches serialization of the <see cref="IAliasAction"/> marker interface to the concrete
	/// action's own interface contract (<see cref="IAliasAddAction"/>, <see cref="IAliasRemoveAction"/>,
	/// <see cref="IAliasRemoveIndexAction"/>). Without this, serializing through the marker interface
	/// (e.g. as an element of <c>IList&lt;IAliasAction&gt;</c>) yields an empty object.
	/// </summary>
	internal sealed class AliasActionConverter : JsonConverter<IAliasAction>
	{
		public override IAliasAction Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			using var doc = JsonDocument.ParseValue(ref reader);
			var root = doc.RootElement;

			if (root.TryGetProperty("add", out _))
				return root.Deserialize<AliasAddAction>(options);
			if (root.TryGetProperty("remove_index", out _))
				return root.Deserialize<AliasRemoveIndexAction>(options);
			if (root.TryGetProperty("remove", out _))
				return root.Deserialize<AliasRemoveAction>(options);

			return null;
		}

		public override void Write(Utf8JsonWriter writer, IAliasAction value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			switch (value)
			{
				case IAliasAddAction add:
					JsonSerializer.Serialize(writer, add, typeof(IAliasAddAction), options);
					break;
				case IAliasRemoveIndexAction removeIndex:
					JsonSerializer.Serialize(writer, removeIndex, typeof(IAliasRemoveIndexAction), options);
					break;
				case IAliasRemoveAction remove:
					JsonSerializer.Serialize(writer, remove, typeof(IAliasRemoveAction), options);
					break;
				default:
					JsonSerializer.Serialize(writer, value, value.GetType(), options);
					break;
			}
		}
	}
}
