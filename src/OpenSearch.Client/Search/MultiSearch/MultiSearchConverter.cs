/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Serializes an <see cref="IMultiSearchRequest"/> as newline-delimited JSON (NDJSON): for each
	/// operation, a header line (index/search_type/preference/routing/ignore_unavailable) followed by
	/// the search body line. Replaces the Utf8Json <c>MultiSearchFormatter</c>.
	/// </summary>
	internal sealed class MultiSearchConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public MultiSearchConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeof(IMultiSearchRequest).IsAssignableFrom(typeToConvert);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var converterType = typeof(MultiSearchConverter<>).MakeGenericType(typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}
	}

	internal sealed class MultiSearchConverter<T> : JsonConverter<T>
		where T : class, IMultiSearchRequest
	{
		private const byte Newline = (byte)'\n';

		private readonly IConnectionSettingsValues _settings;

		public MultiSearchConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			// Reading NDJSON multi-search requests is not supported (matches Utf8Json behavior
			// which round-tripped through the dynamic object resolver and is not exercised).
			reader.Skip();
			return null;
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			if (value?.Operations == null)
			{
				writer.WriteNullValue();
				return;
			}

			var serializer = _settings.RequestResponseSerializer;
			var ndjson = new StringBuilder();

			foreach (var operation in value.Operations.Values)
			{
				var p = operation.RequestParameters;

				string GetString(string key) => p.GetResolvedQueryStringValue(key, _settings);

				IUrlParameter indices = value.Index == null || !value.Index.Equals(operation.Index)
					? operation.Index
					: null;
				var operationIndex = indices?.GetString(_settings);

				var searchType = GetString("search_type");
				if (searchType == "query_then_fetch")
					searchType = null;

				var header = new
				{
					index = operationIndex,
					search_type = searchType,
					preference = GetString("preference"),
					routing = GetString("routing"),
					ignore_unavailable = GetString("ignore_unavailable")
				};

				ndjson.Append(serializer.SerializeToString(header, _settings.MemoryStreamFactory, SerializationFormatting.None));
				ndjson.Append('\n');
				ndjson.Append(serializer.SerializeToString(operation, _settings.MemoryStreamFactory, SerializationFormatting.None));
				ndjson.Append('\n');
			}

			if (ndjson.Length == 0)
			{
				// No operations: emit an empty object so the serializer produces a valid JSON token.
				writer.WriteStartObject();
				writer.WriteEndObject();
				return;
			}

			writer.WriteRawValue(Encoding.UTF8.GetBytes(ndjson.ToString()), skipInputValidation: true);
		}
	}

	/// <summary>
	/// Serializes an <see cref="IMultiSearchTemplateRequest"/> as newline-delimited JSON (NDJSON).
	/// Replaces the Utf8Json <c>MultiSearchTemplateFormatter</c>.
	/// </summary>
	internal sealed class MultiSearchTemplateConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public MultiSearchTemplateConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeof(IMultiSearchTemplateRequest).IsAssignableFrom(typeToConvert);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var converterType = typeof(MultiSearchTemplateConverter<>).MakeGenericType(typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}
	}

	internal sealed class MultiSearchTemplateConverter<T> : JsonConverter<T>
		where T : class, IMultiSearchTemplateRequest
	{
		private readonly IConnectionSettingsValues _settings;

		public MultiSearchTemplateConverter(IConnectionSettingsValues settings) => _settings = settings;

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			reader.Skip();
			return null;
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			if (value?.Operations == null)
			{
				writer.WriteNullValue();
				return;
			}

			var serializer = _settings.RequestResponseSerializer;
			var ndjson = new StringBuilder();

			foreach (var operation in value.Operations.Values)
			{
				var p = operation.RequestParameters;

				string GetString(string key) => p.GetResolvedQueryStringValue(key, _settings);

				IUrlParameter indices = value.Index == null || !value.Index.Equals(operation.Index)
					? operation.Index
					: null;
				var operationIndex = indices?.GetString(_settings);

				var searchType = GetString("search_type");
				if (searchType == "query_then_fetch")
					searchType = null;

				var header = new
				{
					index = operationIndex,
					search_type = searchType,
					preference = GetString("preference"),
					routing = GetString("routing"),
					ignore_unavailable = GetString("ignore_unavailable")
				};

				ndjson.Append(serializer.SerializeToString(header, _settings.MemoryStreamFactory, SerializationFormatting.None));
				ndjson.Append('\n');
				ndjson.Append(serializer.SerializeToString(operation, _settings.MemoryStreamFactory, SerializationFormatting.None));
				ndjson.Append('\n');
			}

			if (ndjson.Length == 0)
			{
				// No operations: emit an empty object so the serializer produces a valid JSON token.
				writer.WriteStartObject();
				writer.WriteEndObject();
				return;
			}

			writer.WriteRawValue(Encoding.UTF8.GetBytes(ndjson.ToString()), skipInputValidation: true);
		}
	}
}
