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
	internal sealed class TaskIdConverter : JsonConverter<TaskId>
	{
		public override TaskId Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			var value = reader.GetString();
			return value == null ? null : new TaskId(value);
		}

		public override void Write(Utf8JsonWriter writer, TaskId value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStringValue(value.FullyQualifiedId);
		}

		public override TaskId ReadAsPropertyName(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var value = reader.GetString();
			return value == null ? null : new TaskId(value);
		}

		public override void WriteAsPropertyName(Utf8JsonWriter writer, TaskId value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WritePropertyName(string.Empty);
				return;
			}

			writer.WritePropertyName(value.FullyQualifiedId);
		}
	}
}
