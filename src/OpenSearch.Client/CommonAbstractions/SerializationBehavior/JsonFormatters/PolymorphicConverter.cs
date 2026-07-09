/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A reusable base for polymorphic (de)serialization of an interface/abstract base
	/// <typeparamref name="TBase"/> whose JSON form identifies the concrete type by a discriminator
	/// (a "type" field, the single wrapper-object key, presence of a field, etc.).
	/// <para>
	/// Intended to be attached to the base type via <c>[JsonConverter(typeof(MyConverter))]</c> so it is
	/// honored in every System.Text.Json nesting context — including collection elements and union
	/// members — where an options-registered converter would be skipped under a custom
	/// <see cref="System.Text.Json.Serialization.Metadata.DefaultJsonTypeInfoResolver"/>.
	/// </para>
	/// <para>
	/// Subclasses supply the discriminator via the protected constructor and register tag→type mappings
	/// with <see cref="Map"/>. The default <see cref="Write"/> serializes the concrete runtime type
	/// (sufficient for types whose JSON is just their own object body, e.g. mapping properties). Types
	/// whose JSON is wrapped (e.g. <c>{ "&lt;field&gt;": { body } }</c>) override <see cref="Write"/>
	/// and/or <see cref="Read"/>.
	/// </para>
	/// </summary>
	internal abstract class PolymorphicConverter<TBase> : JsonConverter<TBase>
		where TBase : class
	{
		private static readonly ConditionalWeakTable<JsonSerializerOptions, JsonSerializerOptions> BodyOptionsCache = new();

		private readonly Func<JsonElement, string> _discriminator;
		private readonly Dictionary<string, Type> _registry = new(StringComparer.OrdinalIgnoreCase);

		protected PolymorphicConverter(Func<JsonElement, string> discriminator) =>
			_discriminator = discriminator ?? throw new ArgumentNullException(nameof(discriminator));

		/// <summary>Registers a discriminator <paramref name="tag"/> to a concrete <paramref name="type"/>.</summary>
		protected PolymorphicConverter<TBase> Map(string tag, Type type)
		{
			_registry[tag] = type;
			return this;
		}

		/// <summary>The concrete type resolved for a discriminator tag, or <c>null</c> if unmapped.</summary>
		protected Type ResolveType(string tag) =>
			tag != null && _registry.TryGetValue(tag, out var t) ? t : null;

		/// <summary>The default concrete type used when the discriminator is missing/unmapped. Null by default.</summary>
		protected virtual Type FallbackType => null;

		/// <summary>
		/// An options instance identical to <paramref name="options"/> but with this converter removed for
		/// <typeparamref name="TBase"/>, so the concrete body can be (de)serialized via its normal contract
		/// without re-entering this converter. Because the converter is attached by attribute on
		/// <typeparamref name="TBase"/>, serializing a concrete subtype does not re-trigger it; this clone
		/// is a safety net for cases where the concrete type is referenced as <typeparamref name="TBase"/>.
		/// </summary>
		protected static JsonSerializerOptions BodyOptions(JsonSerializerOptions options) => options;

		public override TBase Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			if (reader.TokenType == JsonTokenType.Null)
				return null;

			using var document = JsonDocument.ParseValue(ref reader);
			var root = document.RootElement;

			var tag = _discriminator(root);
			var concreteType = ResolveType(tag) ?? FallbackType;
			if (concreteType == null)
				return null;

			return ReadConcrete(root, concreteType, tag, options);
		}

		/// <summary>
		/// Deserializes the discriminated JSON <paramref name="root"/> into <paramref name="concreteType"/>.
		/// Override for wrapped shapes; the default deserializes the object as-is.
		/// </summary>
		protected virtual TBase ReadConcrete(JsonElement root, Type concreteType, string tag, JsonSerializerOptions options) =>
			(TBase)root.Deserialize(concreteType, options);

		public override void Write(Utf8JsonWriter writer, TBase value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// Default: serialize the concrete runtime type's own object body.
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}
	}
}
