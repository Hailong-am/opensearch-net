/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// Converts <see cref="ILazyDocument"/> / <see cref="LazyDocument"/>. On read the raw JSON is
	/// captured verbatim (as UTF-8 bytes) so it can later be deserialized on demand via the source or
	/// request/response serializer. On write the captured bytes are emitted unchanged. Ports the
	/// Utf8Json <c>LazyDocumentFormatter</c>.
	/// </summary>
	internal sealed class LazyDocumentConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public LazyDocumentConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeToConvert == typeof(ILazyDocument) || typeToConvert == typeof(LazyDocument);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			typeToConvert == typeof(ILazyDocument)
				? new LazyDocumentConverter<ILazyDocument>(_settings)
				: new LazyDocumentConverter<LazyDocument>(_settings);
	}

	internal sealed class LazyDocumentConverter<T> : JsonConverter<T>
		where T : class, ILazyDocument
	{
		private static readonly ConstructorInfo Ctor = typeof(LazyDocument).GetConstructor(
			BindingFlags.NonPublic | BindingFlags.Instance,
			binder: null,
			new[] { typeof(byte[]), typeof(IConnectionSettingsValues) },
			modifiers: null);

		private readonly IConnectionSettingsValues _settings;

		public LazyDocumentConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			using var doc = JsonDocument.ParseValue(ref reader);
			using var ms = _settings.MemoryStreamFactory.Create();
			using (var writer = new Utf8JsonWriter(ms))
			{
				doc.RootElement.WriteTo(writer);
				writer.Flush();
			}
			var bytes = ms.ToArray();

			return (T)Ctor.Invoke(new object[] { bytes, _settings });
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			if (value is LazyDocument lazy && lazy.Bytes != null)
				writer.WriteRawValue(lazy.Bytes, skipInputValidation: true);
			else
				writer.WriteNullValue();
		}
	}
}
