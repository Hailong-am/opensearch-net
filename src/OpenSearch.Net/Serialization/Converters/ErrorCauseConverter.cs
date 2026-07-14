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

namespace OpenSearch.Net
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="ErrorCause"/> that handles the complex
	/// deserialization logic from the OpenSearch error response format.
	/// Supports both string format (just reason) and full object format.
	/// </summary>
	internal class ErrorCauseConverter : ErrorCauseConverter<ErrorCause>
	{
	}

	internal class ErrorCauseConverter<TErrorCause> : JsonConverter<TErrorCause>
		where TErrorCause : ErrorCause, new()
	{
		public override TErrorCause Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
			{
				reader.Read();
				return null;
			}

			if (reader.TokenType == JsonTokenType.String)
			{
				return new TErrorCause { Reason = reader.GetString() };
			}

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var errorCause = new TErrorCause();
			var additionalProperties = new Dictionary<string, object>();
			errorCause.AdditionalProperties = additionalProperties;

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var propertyName = reader.GetString();
				reader.Read();

				if (!DeserializeProperty(ref reader, propertyName, errorCause, options))
				{
					// Unknown property — store in AdditionalProperties
					additionalProperties[propertyName] = ReadValue(ref reader);
				}
			}

			return errorCause;
		}

		protected virtual bool DeserializeProperty(ref Utf8JsonReader reader, string propertyName, TErrorCause errorCause, JsonSerializerOptions options)
		{
			return DeserializeErrorCauseProperty(ref reader, propertyName, errorCause, options);
		}

		protected static bool DeserializeErrorCauseProperty(ref Utf8JsonReader reader, string propertyName, TErrorCause errorCause, JsonSerializerOptions options)
		{
			switch (propertyName)
			{
				case "bytes_limit":
					errorCause.BytesLimit = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt64();
					return true;
				case "bytes_wanted":
					errorCause.BytesWanted = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt64();
					return true;
				case "caused_by":
					errorCause.CausedBy = JsonSerializer.Deserialize<ErrorCause>(ref reader, options);
					return true;
				case "col":
					errorCause.Column = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
					return true;
				case "failed_shards":
					errorCause.FailedShards = JsonSerializer.Deserialize<List<ShardFailure>>(ref reader, options);
					return true;
				case "grouped":
					errorCause.Grouped = reader.TokenType == JsonTokenType.Null ? null : reader.GetBoolean();
					return true;
				case "index":
					errorCause.Index = reader.GetString();
					return true;
				case "index_uuid":
					errorCause.IndexUUID = reader.GetString();
					return true;
				case "lang":
					errorCause.Language = reader.GetString();
					return true;
				case "line":
					errorCause.Line = reader.TokenType == JsonTokenType.Null ? null : reader.GetInt32();
					return true;
				case "phase":
					errorCause.Phase = reader.GetString();
					return true;
				case "reason":
					errorCause.Reason = reader.GetString();
					return true;
				case "resource.id":
					errorCause.ResourceId = ReadStringOrStringArray(ref reader);
					return true;
				case "resource.type":
					errorCause.ResourceType = reader.GetString();
					return true;
				case "script":
					errorCause.Script = reader.GetString();
					return true;
				case "script_stack":
					errorCause.ScriptStack = ReadStringOrStringArray(ref reader);
					return true;
				case "shard":
					errorCause.Shard = ReadNullableIntFromStringOrNumber(ref reader);
					return true;
				case "stack_trace":
					errorCause.StackTrace = reader.GetString();
					return true;
				case "type":
					errorCause.Type = reader.GetString();
					return true;
				default:
					return false;
			}
		}

		protected static IReadOnlyCollection<string> ReadStringOrStringArray(ref Utf8JsonReader reader)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return Array.Empty<string>();

			if (reader.TokenType == JsonTokenType.String)
				return new[] { reader.GetString() };

			if (reader.TokenType == JsonTokenType.StartArray)
			{
				var list = new List<string>();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				{
					list.Add(reader.GetString());
				}
				return list;
			}

			reader.Skip();
			return Array.Empty<string>();
		}

		protected static int? ReadNullableIntFromStringOrNumber(ref Utf8JsonReader reader)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;
			if (reader.TokenType == JsonTokenType.Number)
				return reader.GetInt32();
			if (reader.TokenType == JsonTokenType.String)
			{
				var s = reader.GetString();
				if (int.TryParse(s, out var val))
					return val;
				return null;
			}
			reader.Skip();
			return null;
		}

		protected static object ReadValue(ref Utf8JsonReader reader)
		{
			switch (reader.TokenType)
			{
				case JsonTokenType.String:
					return reader.GetString();
				case JsonTokenType.Number:
					if (reader.TryGetInt64(out var l))
						return l;
					return reader.GetDouble();
				case JsonTokenType.True:
					return true;
				case JsonTokenType.False:
					return false;
				case JsonTokenType.Null:
					return null;
				case JsonTokenType.StartArray:
				case JsonTokenType.StartObject:
					// Read as JsonElement for complex types
					using (var doc = JsonDocument.ParseValue(ref reader))
						return doc.RootElement.Clone();
				default:
					reader.Skip();
					return null;
			}
		}

		public override void Write(Utf8JsonWriter writer, TErrorCause value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			WriteErrorCauseProperties(writer, value, options);
			WriteAdditionalProperties(writer, value, options);

			writer.WriteEndObject();
		}

		protected virtual void WriteAdditionalProperties(Utf8JsonWriter writer, TErrorCause value, JsonSerializerOptions options)
		{
			if (value.AdditionalProperties != null)
			{
				foreach (var kvp in value.AdditionalProperties)
				{
					writer.WritePropertyName(kvp.Key);
					JsonSerializer.Serialize(writer, kvp.Value, options);
				}
			}
		}

		protected static void WriteErrorCauseProperties(Utf8JsonWriter writer, TErrorCause value, JsonSerializerOptions options)
		{
			if (value.BytesLimit.HasValue)
			{
				writer.WriteNumber("bytes_limit", value.BytesLimit.Value);
			}

			if (value.BytesWanted.HasValue)
			{
				writer.WriteNumber("bytes_wanted", value.BytesWanted.Value);
			}

			if (value.CausedBy != null)
			{
				writer.WritePropertyName("caused_by");
				JsonSerializer.Serialize(writer, value.CausedBy, options);
			}

			if (value.Column.HasValue)
			{
				writer.WriteNumber("col", value.Column.Value);
			}

			if (value.FailedShards != null && ((System.Collections.ICollection)value.FailedShards).Count > 0)
			{
				writer.WritePropertyName("failed_shards");
				JsonSerializer.Serialize(writer, value.FailedShards, options);
			}

			if (value.Grouped.HasValue)
			{
				writer.WriteBoolean("grouped", value.Grouped.Value);
			}

			if (value.Index != null)
			{
				writer.WriteString("index", value.Index);
			}

			if (value.IndexUUID != null)
			{
				writer.WriteString("index_uuid", value.IndexUUID);
			}

			if (value.Language != null)
			{
				writer.WriteString("lang", value.Language);
			}

			if (value.Line.HasValue)
			{
				writer.WriteNumber("line", value.Line.Value);
			}

			if (value.Phase != null)
			{
				writer.WriteString("phase", value.Phase);
			}

			if (value.Reason != null)
			{
				writer.WriteString("reason", value.Reason);
			}

			if (value.ResourceId != null && ((System.Collections.ICollection)value.ResourceId).Count > 0)
			{
				writer.WritePropertyName("resource.id");
				JsonSerializer.Serialize(writer, value.ResourceId, options);
			}

			if (value.ResourceType != null)
			{
				writer.WriteString("resource.type", value.ResourceType);
			}

			if (value.Script != null)
			{
				writer.WriteString("script", value.Script);
			}

			if (value.ScriptStack != null && ((System.Collections.ICollection)value.ScriptStack).Count > 0)
			{
				writer.WritePropertyName("script_stack");
				JsonSerializer.Serialize(writer, value.ScriptStack, options);
			}

			if (value.Shard.HasValue)
			{
				writer.WriteNumber("shard", value.Shard.Value);
			}

			if (value.StackTrace != null)
			{
				writer.WriteString("stack_trace", value.StackTrace);
			}

			if (value.Type != null)
			{
				writer.WriteString("type", value.Type);
			}
		}
	}
}
