/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Runtime.Serialization;
using OpenSearch.Net.Utf8Json;
using OpenSearch.Net.Utf8Json.Internal;

namespace OpenSearch.Net
{
	internal class ErrorFormatter : ErrorCauseFormatter<Error>
	{
		private static readonly AutomataDictionary Fields = new AutomataDictionary
		{
			{ "headers", 0 },
			{ "root_cause", 1 }
		};

		protected override void Serialize(ref JsonWriter writer, ref int count, Error value, IJsonFormatterResolver formatterResolver)
		{
			if (value.Headers.Any())
			{
				if (count > 0)
					writer.WriteValueSeparator();

				writer.WritePropertyName("headers");
				formatterResolver.GetFormatter<IReadOnlyDictionary<string, string>>()
					.Serialize(ref writer, value.Headers, formatterResolver);

				count++;
			}

			if (value.RootCause.Any())
			{
				if (count > 0)
					writer.WriteValueSeparator();

				writer.WritePropertyName("root_cause");
				formatterResolver.GetFormatter<IReadOnlyCollection<ErrorCause>>()
					.Serialize(ref writer, value.RootCause, formatterResolver);

				count++;
			}
		}

		protected override bool Deserialize(ref JsonReader reader, ref ArraySegment<byte> property, Error value, IJsonFormatterResolver formatterResolver)
		{
			if (Fields.TryGetValue(property, out var fieldValue))
			{
				switch (fieldValue)
				{
					case 0:
						value.Headers = formatterResolver.GetFormatter<Dictionary<string, string>>()
							.Deserialize(ref reader, formatterResolver);
						break;
					case 1:
						value.RootCause = formatterResolver.GetFormatter<List<ErrorCause>>()
							.Deserialize(ref reader, formatterResolver);
						break;
				}

				return true;
			}

			return false;
		}
	}

}
