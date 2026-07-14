/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	internal sealed class BulkRequestConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public BulkRequestConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) =>
			typeToConvert == typeof(IBulkRequest)
			|| typeToConvert == typeof(BulkRequest)
			|| typeToConvert == typeof(BulkDescriptor);

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options) =>
			(JsonConverter)Activator.CreateInstance(
				typeof(BulkRequestConverter<>).MakeGenericType(typeToConvert), _settings);
	}

	/// <summary>
	/// Serializes an <see cref="IBulkRequest"/> to the newline-delimited JSON (NDJSON) bulk format:
	/// for each operation an action-metadata line (<c>{ "index": { ... } }</c>) is written, followed
	/// by the operation body line (the source document, or the update body) when present.
	/// Ports the Utf8Json <c>BulkRequestFormatter</c>.
	/// </summary>
	/// <remarks>
	/// The bulk body is a sequence of top-level JSON documents separated by <c>\n</c>, which is not a
	/// single valid JSON document. The whole payload is therefore assembled in a buffer and emitted in
	/// one <see cref="Utf8JsonWriter.WriteRawValue(System.ReadOnlySpan{byte}, bool)"/> call (with input
	/// validation disabled) so the writer treats it as a single opaque value rather than inserting
	/// element separators between the individual lines.
	/// </remarks>
	internal sealed class BulkRequestConverter<T> : JsonConverter<T>
		where T : IBulkRequest
	{
		private const byte Newline = (byte)'\n';
		private static readonly byte[] EmptyBody = { Newline };

		private readonly IConnectionSettingsValues _settings;

		public BulkRequestConverter(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options) =>
			throw new NotSupportedException();

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			var request = (IBulkRequest)value;

			var inferrer = _settings.Inferrer;

			using var ms = _settings.MemoryStreamFactory.Create();

			for (var i = 0; request?.Operations != null && i < request.Operations.Count; i++)
			{
				var op = request.Operations[i];

				op.Index ??= request.Index ?? op.ClrType;
				if (op.Index.Equals(request.Index)) op.Index = null;
				op.Id = op.GetIdForOperation(inferrer);

				// GetRoutingForOperation falls back to deriving routing from the document; for
				// operations without a document (e.g. a delete by id) this yields a Routing that
				// resolves to null. STJ's WhenWritingNull only skips null CLR references, so null out
				// the property when it resolves empty to avoid emitting "routing": null.
				var routing = op.GetRoutingForOperation(inferrer);
				op.Routing = string.IsNullOrEmpty(((IUrlParameter)routing)?.GetString(_settings)) ? null : routing;

				// Action-metadata line: { "<operation>": { ...metadata... } }
				using (var w = new Utf8JsonWriter(ms))
				{
					w.WriteStartObject();
					w.WritePropertyName(op.Operation);
					JsonSerializer.Serialize(w, op, op.GetType(), options);
					w.WriteEndObject();
				}
				ms.WriteByte(Newline);

				var body = op.GetBody();
				if (body == null)
					continue;

				// Body line. Update bodies and lazy documents go through the high-level serializer;
				// user documents are handled by the source serializer via SourceConverter — both are
				// reachable through the same domain options.
				using (var w = new Utf8JsonWriter(ms))
					JsonSerializer.Serialize(w, body, body.GetType(), options);
				ms.WriteByte(Newline);
			}

			var payload = ms.ToArray();
			// An empty bulk request has an empty body; WriteRawValue rejects empty input, so emit a
			// single newline (the server tolerates a blank bulk body) to keep the writer valid.
			writer.WriteRawValue(payload.Length == 0 ? EmptyBody : payload, skipInputValidation: true);
		}
	}
}
