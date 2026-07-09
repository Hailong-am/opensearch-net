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
	/// A <see cref="JsonConverter{T}"/> for <see cref="Error"/> that handles the complex
	/// deserialization logic from the OpenSearch error response format.
	/// Extends the base ErrorCause converter with headers and root_cause fields.
	/// </summary>
	internal class ErrorConverter : ErrorCauseConverter<Error>
	{
		protected override bool DeserializeProperty(ref Utf8JsonReader reader, string propertyName, Error errorCause, JsonSerializerOptions options)
		{
			switch (propertyName)
			{
				case "headers":
					errorCause.Headers = JsonSerializer.Deserialize<Dictionary<string, string>>(ref reader, options)
						?? new Dictionary<string, string>();
					return true;
				case "root_cause":
					errorCause.RootCause = JsonSerializer.Deserialize<List<ErrorCause>>(ref reader, options);
					return true;
				default:
					return DeserializeErrorCauseProperty(ref reader, propertyName, errorCause, options);
			}
		}

		public override void Write(Utf8JsonWriter writer, Error value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			WriteErrorCauseProperties(writer, value, options);

			if (value.Headers != null && value.Headers.Count > 0)
			{
				writer.WritePropertyName("headers");
				JsonSerializer.Serialize(writer, value.Headers, options);
			}

			if (value.RootCause != null && ((System.Collections.ICollection)value.RootCause).Count > 0)
			{
				writer.WritePropertyName("root_cause");
				JsonSerializer.Serialize(writer, value.RootCause, options);
			}

			WriteAdditionalProperties(writer, value, options);

			writer.WriteEndObject();
		}
	}
}
