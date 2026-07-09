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
	internal sealed class ScriptConverter : JsonConverter<IScript>
	{
		public override IScript Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			string source = null;
			string id = null;
			string lang = null;
			Dictionary<string, object> scriptParams = null;

			while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
			{
				if (reader.TokenType != JsonTokenType.PropertyName)
					continue;

				var propertyName = reader.GetString();
				reader.Read();

				switch (propertyName)
				{
					case "source":
					case "inline":
						source = reader.GetString();
						break;
					case "id":
						id = reader.GetString();
						break;
					case "lang":
						lang = reader.GetString();
						break;
					case "params":
						scriptParams = JsonSerializer.Deserialize<Dictionary<string, object>>(ref reader, options);
						break;
					default:
						reader.Skip();
						break;
				}
			}

			IScript script;
			if (id != null)
				script = new IndexedScript(id);
			else if (source != null)
				script = new InlineScript(source);
			else
				return null;

			script.Lang = lang;
			script.Params = scriptParams;
			return script;
		}

		public override void Write(Utf8JsonWriter writer, IScript value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			switch (value)
			{
				case IInlineScript inline:
					JsonSerializer.Serialize(writer, inline, typeof(IInlineScript), options);
					break;
				case IIndexedScript indexed:
					JsonSerializer.Serialize(writer, indexed, typeof(IIndexedScript), options);
					break;
				default:
					JsonSerializer.Serialize(writer, value, value.GetType(), options);
					break;
			}
		}
	}
}
