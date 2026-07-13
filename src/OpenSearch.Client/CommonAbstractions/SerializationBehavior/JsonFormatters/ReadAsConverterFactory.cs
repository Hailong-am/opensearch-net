/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Collections.Concurrent;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> that implements the equivalent of Utf8Json's
	/// <c>[InterfaceDataContract]</c> + <c>[ReadAs(typeof(ConcreteType))]</c> pattern.
	/// When a type (typically an interface) has a <see cref="ReadAsAttribute"/>, this factory
	/// creates a converter that deserializes JSON into the specified concrete implementation type
	/// and serializes using the actual runtime type.
	/// </summary>
	internal sealed class ReadAsConverterFactory : JsonConverterFactory
	{
		private static readonly ConcurrentDictionary<Type, JsonConverter> ConverterCache = new();

		/// <inheritdoc />
		public override bool CanConvert(Type typeToConvert)
		{
			var readAsAttribute = typeToConvert.GetCustomAttribute<ReadAsAttribute>();
			if (readAsAttribute == null)
				return false;

			// A type may carry both [ReadAs] (for the Utf8Json engine) and an explicit [JsonConverter]
			// (for System.Text.Json) — e.g. IBoolQuery. When an explicit STJ converter is present it must
			// win here, otherwise this factory would shadow it and change STJ output.
			var jsonConverterAttribute = typeToConvert.GetCustomAttribute<JsonConverterAttribute>();
			return jsonConverterAttribute == null;
		}

		/// <inheritdoc />
		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			return ConverterCache.GetOrAdd(typeToConvert, type =>
			{
				var readAsAttribute = type.GetCustomAttribute<ReadAsAttribute>();
				if (readAsAttribute == null)
					throw new InvalidOperationException(
						$"Type {type.FullName} does not have a {nameof(ReadAsAttribute)}.");

				Type concreteType;
				if (readAsAttribute.Type.IsGenericType && !readAsAttribute.Type.IsConstructedGenericType)
				{
					// Handle open generic types: e.g., [ReadAs(typeof(HasChildQueryDescriptor<>))]
					// Construct the generic type using the interface's own type arguments
					concreteType = readAsAttribute.Type.MakeGenericType(type.GenericTypeArguments);
				}
				else
				{
					concreteType = readAsAttribute.Type;
				}

				var converterType = typeof(ReadAsConverter<,>).MakeGenericType(type, concreteType);
				return (JsonConverter)Activator.CreateInstance(converterType);
			});
		}
	}

	/// <summary>
	/// A <see cref="JsonConverter{T}"/> that deserializes JSON as a concrete type <typeparamref name="TConcrete"/>
	/// but exposes it as the interface/abstract type <typeparamref name="TInterface"/>.
	/// This replaces the Utf8Json <c>ReadAsFormatter&lt;TRead, T&gt;</c>.
	/// </summary>
	/// <typeparam name="TInterface">The interface or abstract type requested for deserialization.</typeparam>
	/// <typeparam name="TConcrete">The concrete implementation type to deserialize into.</typeparam>
	internal sealed class ReadAsConverter<TInterface, TConcrete> : JsonConverter<TInterface>
		where TConcrete : TInterface
	{
		public override TInterface Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			// Deserialize as the concrete implementation type
			return JsonSerializer.Deserialize<TConcrete>(ref reader, options);
		}

		public override void Write(Utf8JsonWriter writer, TInterface value, JsonSerializerOptions options)
		{
			if (value == null)
			{
				writer.WriteNullValue();
				return;
			}

			// Serialize using the actual runtime type to ensure all properties are written
			JsonSerializer.Serialize(writer, value, value.GetType(), options);
		}
	}

}
