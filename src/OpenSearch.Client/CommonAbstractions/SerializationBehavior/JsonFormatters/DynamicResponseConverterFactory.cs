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
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Deserializes responses deriving from <see cref="DynamicResponseBase"/> (e.g.
	/// <see cref="ClusterStateResponse"/>) by reading the whole JSON body into the response's
	/// <see cref="DynamicDictionary"/> backing store, peeling off <c>error</c>/<c>status</c> into the
	/// <see cref="ResponseBase"/> error members. Replaces the pre-STJ <c>DynamicResponseFormatter&lt;T&gt;</c>.
	/// Without it these responses deserialize via STJ's default object contract, which never populates the
	/// backing dictionary — so computed accessors such as <c>ClusterStateResponse.ClusterName</c> return null.
	/// </summary>
	internal sealed class DynamicResponseConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public DynamicResponseConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			!typeToConvert.IsAbstract && !typeToConvert.IsInterface && typeof(DynamicResponseBase).IsAssignableFrom(typeToConvert);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var converterType = typeof(DynamicResponseConverter<>).MakeGenericType(typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}
	}

	internal sealed class DynamicResponseConverter<TResponse> : JsonConverter<TResponse>
		where TResponse : DynamicResponseBase, new()
	{
		private readonly IConnectionSettingsValues _settings;

		public DynamicResponseConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override TResponse Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			if (reader.TokenType != JsonTokenType.StartObject)
				throw new JsonException($"Expected StartObject, got {reader.TokenType}");

			var response = new TResponse();
			var dictionary = new Dictionary<string, object>();

			reader.Read();
			while (reader.TokenType != JsonTokenType.EndObject)
			{
				var property = reader.GetString();
				reader.Read();

				if (property == "error")
				{
					if (reader.TokenType == JsonTokenType.String)
						response.Error = new Error { Reason = reader.GetString() };
					else
						response.Error = JsonSerializer.Deserialize<Error>(ref reader, options);
				}
				else if (property == "status" && reader.TokenType == JsonTokenType.Number)
				{
					response.StatusCode = reader.GetInt32();
				}
				else
				{
					// Values deserialize via the registered ObjectConverter into .NET primitives / nested
					// Dictionary<string,object> / List<object>, which DynamicDictionary.Create wraps.
					dictionary[property] = JsonSerializer.Deserialize<object>(ref reader, options);
				}

				reader.Read();
			}

			((IDynamicResponse)response).BackingDictionary = DynamicDictionary.Create(dictionary);
			return response;
		}

		public override void Write(Utf8JsonWriter writer, TResponse value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			var backing = ((IDynamicResponse)value).BackingDictionary;
			JsonSerializer.Serialize(writer, backing, options);
		}
	}
}
