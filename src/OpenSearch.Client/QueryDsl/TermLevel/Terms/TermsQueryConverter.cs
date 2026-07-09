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
	/// <summary>
	/// A <see cref="JsonConverter{T}"/> for <see cref="ITermsQuery"/> that handles
	/// the non-standard JSON shape where the field name is a dynamic key, and the value
	/// can be either an array of terms or a terms lookup object.
	/// <para>
	/// Example JSON shapes:
	/// <code>
	/// { "_name": "named_query", "boost": 1.1, "field_name": ["val1", "val2"] }
	/// { "field_name": { "id": "1", "index": "my-index", "path": "color" } }
	/// </code>
	/// </para>
	/// </summary>
	internal sealed class TermsQueryConverter : JsonConverter<ITermsQuery>
	{
		private static readonly JsonEncodedText BoostProp = JsonEncodedText.Encode("boost");
		private static readonly JsonEncodedText NameProp = JsonEncodedText.Encode("_name");

		private readonly IConnectionSettingsValues _settings;

		public TermsQueryConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override ITermsQuery Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
			{
				reader.Skip();
				return null;
			}

			var query = new TermsQuery();

			while (reader.Read())
			{
				if (reader.TokenType == JsonTokenType.EndObject)
					break;

				if (reader.TokenType != JsonTokenType.PropertyName)
					break;

				var propertyName = reader.GetString();
				reader.Read(); // Move to value

				switch (propertyName)
				{
					case "boost":
						query.Boost = reader.GetDouble();
						break;
					case "_name":
						query.Name = reader.GetString();
						break;
					default:
						// This is the field name — value is either an array of terms or a lookup object
						query.Field = propertyName;
						ReadTermsOrLookup(ref reader, query, options);
						break;
				}
			}

			return query;
		}

		public override void Write(Utf8JsonWriter writer, ITermsQuery value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			writer.WriteStartObject();

			if (!string.IsNullOrEmpty(value.Name))
			{
				writer.WritePropertyName(NameProp);
				writer.WriteStringValue(value.Name);
			}

			if (value.Boost.HasValue)
			{
				writer.WritePropertyName(BoostProp);
				writer.WriteNumberValue(value.Boost.Value);
			}

			// Write the field name and its value (terms array or lookup object)
			var field = value.Field == null ? null : _settings.Inferrer.Field(value.Field);
			if (!string.IsNullOrEmpty(field))
			{
				writer.WritePropertyName(field);

				if (value.IsVerbatim)
				{
					if (value.TermsLookup != null)
						JsonSerializer.Serialize(writer, value.TermsLookup, options);
					else if (value.Terms != null)
						WriteTermsArray(writer, value.Terms, options);
					else
						writer.WriteNullValue();
				}
				else
				{
					if (value.Terms != null)
						WriteTermsArray(writer, value.Terms, options);
					else if (value.TermsLookup != null)
						JsonSerializer.Serialize(writer, value.TermsLookup, options);
					else
						writer.WriteNullValue();
				}
			}

			writer.WriteEndObject();
		}

		private static void ReadTermsOrLookup(ref Utf8JsonReader reader, TermsQuery query, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.StartArray)
			{
				// Array of term values
				var terms = new List<object>();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
				{
					switch (reader.TokenType)
					{
						case JsonTokenType.String:
							terms.Add(reader.GetString());
							break;
						case JsonTokenType.Number:
							if (reader.TryGetInt64(out var longVal))
								terms.Add(longVal);
							else
								terms.Add(reader.GetDouble());
							break;
						case JsonTokenType.True:
							terms.Add(true);
							break;
						case JsonTokenType.False:
							terms.Add(false);
							break;
						case JsonTokenType.Null:
							terms.Add(null);
							break;
						default:
							// For nested objects/arrays, use JsonElement
							using (var doc = JsonDocument.ParseValue(ref reader))
								terms.Add(doc.RootElement.Clone());
							break;
					}
				}
				query.Terms = terms;
			}
			else if (reader.TokenType == JsonTokenType.StartObject)
			{
				// Terms lookup object
				var fieldLookup = new FieldLookup();
				while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
				{
					if (reader.TokenType != JsonTokenType.PropertyName)
						continue;

					var prop = reader.GetString();
					reader.Read();

					switch (prop)
					{
						case "id":
							fieldLookup.Id = reader.GetString();
							break;
						case "index":
							fieldLookup.Index = reader.GetString();
							break;
						case "path":
							fieldLookup.Path = reader.GetString();
							break;
						case "routing":
							fieldLookup.Routing = reader.GetString();
							break;
						default:
							reader.Skip();
							break;
					}
				}
				query.TermsLookup = fieldLookup;
			}
			else
			{
				reader.Skip();
			}
		}

		private static void WriteTermsArray(Utf8JsonWriter writer, IEnumerable<object> terms, JsonSerializerOptions options)
		{
			writer.WriteStartArray();
			foreach (var term in terms)
				WriteTerm(writer, term, options);
			writer.WriteEndArray();
		}

		private static void WriteTerm(Utf8JsonWriter writer, object term, JsonSerializerOptions options)
		{
			switch (term)
			{
				case null:
					writer.WriteNullValue();
					break;
				// Terms buffered during deserialization (e.g. nested arrays in a list-of-list terms
				// query) are stored as JsonElement; write them verbatim rather than routing through
				// the (source) serializer, which would emit the JsonElement's public shape.
				case JsonElement element:
					element.WriteTo(writer);
					break;
				default:
					JsonSerializer.Serialize(writer, term, term.GetType(), options);
					break;
			}
		}
	}
}
