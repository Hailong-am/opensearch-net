/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Net
{
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="Exception"/> that provides safe serialization
	/// of exception information. Replaces the Utf8Json-based <c>ExceptionFormatter</c>.
	/// </summary>
	/// <remarks>
	/// Serialization writes a JSON array of exception objects (flattened from InnerException chain).
	/// Each element contains type, message, and stackTrace properties.
	/// Deserialization is not supported (returns null) as exceptions are rarely deserialized.
	/// A maximum depth limit of 20 prevents infinite recursion from circular InnerException references.
	/// </remarks>
	internal class ExceptionConverter : JsonConverter<Exception>
	{
		private const int MaxInnerExceptionDepth = 20;

		public override bool CanConvert(Type typeToConvert) =>
			typeof(Exception).IsAssignableFrom(typeToConvert);

		public override Exception Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			// Exceptions are rarely deserialized. Skip the JSON tokens and return null.
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			reader.Skip();
			return null;
		}

		public override void Write(Utf8JsonWriter writer, Exception value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartArray();

			var depth = 0;
			var current = value;

			while (current != null && depth < MaxInnerExceptionDepth)
			{
				writer.WriteStartObject();

				writer.WriteNumber("Depth", depth);
				writer.WriteString("ClassName", current.GetType().FullName);
				writer.WriteString("Message", current.Message);
				writer.WriteNull("Source");
				writer.WriteNull("StackTraceString");
				writer.WriteNull("RemoteStackTraceString");
				writer.WriteNumber("RemoteStackIndex", 0);
				writer.WriteNumber("HResult", current.HResult);
				writer.WriteNull("HelpURL");

				writer.WriteEndObject();

				current = current.InnerException;
				depth++;
			}

			writer.WriteEndArray();
		}
	}
}
