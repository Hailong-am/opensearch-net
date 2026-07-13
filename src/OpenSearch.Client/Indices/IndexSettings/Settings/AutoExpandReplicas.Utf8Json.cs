/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class AutoExpandReplicasFormatter : IJsonFormatter<AutoExpandReplicas>
	{
		public AutoExpandReplicas Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var token = reader.GetCurrentJsonToken();

			if (token == JsonToken.False)
				return AutoExpandReplicas.Disabled;
			if (token == JsonToken.String)
				return AutoExpandReplicas.Create(reader.ReadString());

			throw new Exception($"Cannot deserialize {typeof(AutoExpandReplicas)} from {token}");
		}

		public void Serialize(ref JsonWriter writer, AutoExpandReplicas value, IJsonFormatterResolver formatterResolver)
		{
			if (value == null || !value.Enabled)
			{
				writer.WriteBoolean(false);
				return;
			}

			writer.WriteString(value.ToString());
		}
	}

}
