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

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes <see cref="IClusterRerouteCommand"/> as <c>{ "&lt;command_name&gt;": { ...body... } }</c>
	/// and dispatches deserialization on the single command-name key. Replaces the Utf8Json
	/// <c>ClusterRerouteCommandFormatter</c>.
	/// </summary>
	internal sealed class ClusterRerouteCommandConverter : JsonConverter<IClusterRerouteCommand>
	{
		private static readonly Dictionary<string, Type> CommandTypes = new()
		{
			["allocate_replica"] = typeof(AllocateReplicaClusterRerouteCommand),
			["allocate_empty_primary"] = typeof(AllocateEmptyPrimaryRerouteCommand),
			["allocate_stale_primary"] = typeof(AllocateStalePrimaryRerouteCommand),
			["move"] = typeof(MoveClusterRerouteCommand),
			["cancel"] = typeof(CancelClusterRerouteCommand),
		};

		public override IClusterRerouteCommand Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Cannot deserialize IClusterRerouteCommand from {reader.TokenType}");

			using var doc = JsonDocument.ParseValue(ref reader);
			foreach (var prop in doc.RootElement.EnumerateObject())
			{
				if (CommandTypes.TryGetValue(prop.Name, out var commandType))
					return (IClusterRerouteCommand)JsonSerializer.Deserialize(prop.Value.GetRawText(), commandType, options);
			}

			return null;
		}

		public override void Write(Utf8JsonWriter writer, IClusterRerouteCommand value, JsonSerializerOptions options)
		{
			if (value?.Name == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();
			writer.WritePropertyName(value.Name);
			// Serialize the body by the runtime type so the concrete command's [InterfaceDataContract]
			// property contract is used (not the empty IClusterRerouteCommand interface contract).
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
			writer.WriteEndObject();
		}
	}
}
