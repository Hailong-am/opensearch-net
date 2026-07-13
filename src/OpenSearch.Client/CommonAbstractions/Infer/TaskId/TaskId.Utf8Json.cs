/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Diagnostics;
using System.Globalization;
using OpenSearch.Net;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class TaskIdFormatter : IJsonFormatter<TaskId>, IObjectPropertyNameFormatter<TaskId>
	{
		public TaskId Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			if (reader.GetCurrentJsonToken() == JsonToken.String)
				return new TaskId(reader.ReadString());

			reader.ReadNextBlock();
			return null;
		}

		public void Serialize(ref JsonWriter writer, TaskId value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null)
			{
				writer.WriteNull();
				return;
			}

			writer.WriteString(value.ToString());
		}

		public TaskId DeserializeFromPropertyName(ref JsonReader reader, IJsonFormatterResolver formatterResolver) =>
			Deserialize(ref reader, formatterResolver);

		public void SerializeToPropertyName(ref JsonWriter writer, TaskId value, IJsonFormatterResolver formatterResolver) =>
			Serialize(ref writer, value, formatterResolver);
	}

}
