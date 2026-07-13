/* SPDX-License-Identifier: Apache-2.0 */
// Restored Utf8Json formatter(s) for dual-serializer support. Compiled only for the
// Utf8Json serialization path; STJ ignores [JsonFormatter].
using System;
using System.Collections;
using System.Collections.Generic;
using OpenSearch.Net;
using OpenSearch.Net.Utf8Json;

namespace OpenSearch.Client
{
	internal class ResolvableDictionaryResponseFormatter<TResponse, TKey, TValue> : IJsonFormatter<TResponse>
		where TResponse : ResponseBase, IDictionaryResponse<TKey, TValue>, new()
		where TKey : IUrlParameter
	{
		public TResponse Deserialize(ref JsonReader reader, IJsonFormatterResolver formatterResolver)
		{
			var response = new TResponse();
			var dictionary = new Dictionary<TKey, TValue>();
			var count = 0;
			var keyFormatter = formatterResolver.GetFormatter<TKey>();
			var valueFormatter = formatterResolver.GetFormatter<TValue>();

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

			var settings = formatterResolver.GetConnectionSettings();
			var resolvableDictionary = new ResolvableDictionaryProxy<TKey, TValue>(settings, dictionary);
			response.BackingDictionary = resolvableDictionary;
			return response;
		}

		public void Serialize(ref JsonWriter writer, TResponse value, IJsonFormatterResolver formatterResolver) =>
			throw new NotSupportedException();
	}

}
