/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using OpenSearch.Net;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class DynamicResponseFormatter<TResponse> : IJsonFormatter<TResponse>
		where TResponse : ResponseBase, IDynamicResponse, new()
	{
		public TResponse Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{

			var response = new TResponse();

			var keyFormatter = formatterResolver.GetFormatter<string>();
			var valueFormatter = formatterResolver.GetFormatter<object>();
			var dictionary = new Dictionary<string, object>();
			var count = 0;

			while (reader.ReadIsInObject(ref count))
			{
				var property = reader.ReadPropertyNameSegmentRaw();
				if (ResponseFormatterHelpers.ServerErrorFieldsAutomata.TryGetValue(property, out var errorValue))
				{
					switch (errorValue)
					{
						case 0:
							if (reader.GetCurrentJsonToken() == JsonToken.String)
								response.Error = new Error { Reason = reader.ReadString() };
							else
							{
								var formatter = formatterResolver.GetFormatter<Error>();
								response.Error = formatter.Deserialize(ref reader, formatterResolver);
							}
							break;
						case 1:
							if (reader.GetCurrentJsonToken() == JsonToken.Number)
								response.StatusCode = reader.ReadInt32();
							else
								reader.ReadNextBlock();
							break;
					}
				}
				else
				{
					// include opening string quote in reader (offset - 1)
					var propertyReader = new JsonReader(property.Array, property.Offset - 1);
					var key = keyFormatter.Deserialize(ref propertyReader, formatterResolver);
					var value = valueFormatter.Deserialize(ref reader, formatterResolver);
					dictionary.Add(key, value);
				}
			}

			response.BackingDictionary = DynamicDictionary.Create(dictionary);
			return response;
		}

		public void Serialize(ref JsonWriter writer, TResponse value, IJsonFormatterResolver formatterResolver) => throw new NotSupportedException();
	}

}
