/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;
using OpenSearch.Net;

namespace OpenSearch.Client
{
	/// <summary>
	/// A <see cref="JsonConverterFactory"/> for the terminal source options that delegates OpenSearch.Client
	/// domain-contract types (e.g. <see cref="QueryContainer"/> and the query DSL, mappings, aggregations)
	/// back to the full high-level domain options when they appear as members of a user document.
	/// <para>
	/// The terminal source options (<see cref="SourceConverterHelper.GetDefaultSourceOptions"/>) are
	/// POCO-oriented and intentionally omit the high-level domain converters. Without this delegation an
	/// embedded domain type such as a percolator query stored on <c>ProjectPercolation.Query</c> would emit
	/// an empty object (its query bodies are exposed only through explicit-interface members and require the
	/// field-name-query wrapping converters) — producing a percolator that matches nothing.
	/// </para>
	/// </summary>
	internal sealed class EmbeddedDomainTypeConverterFactory : JsonConverterFactory
	{
		private readonly IConnectionSettingsValues _settings;

		public EmbeddedDomainTypeConverterFactory(IConnectionSettingsValues settings) =>
			_settings = settings ?? throw new ArgumentNullException(nameof(settings));

		public override bool CanConvert(Type typeToConvert) => IsDomainContractType(typeToConvert);

		/// <summary>
		/// Whether <paramref name="type"/> is (or implements) an OpenSearch.Client domain contract — an
		/// interface marked <see cref="InterfaceDataContractAttribute"/>, or a concrete type implementing one.
		/// These must round-trip through the domain machinery rather than plain POCO reflection.
		/// </summary>
		private static bool IsDomainContractType(Type type)
		{
			if (type == null) return false;

			var assemblyName = type.Assembly.GetName().Name;
			var isFrameworkType = assemblyName != null &&
				(assemblyName.StartsWith("OpenSearch.Net", StringComparison.Ordinal) ||
				 assemblyName.StartsWith("OpenSearch.Client", StringComparison.Ordinal));

			// Only OpenSearch framework types (or user types implementing a framework contract) qualify.
			if (type.IsInterface && isFrameworkType && InterfaceDataContract.IsDefinedOn(type))
				return true;

			foreach (var iface in type.GetInterfaces())
			{
				var ifaceAssembly = iface.Assembly.GetName().Name;
				if (ifaceAssembly != null &&
					(ifaceAssembly.StartsWith("OpenSearch.Net", StringComparison.Ordinal) ||
					 ifaceAssembly.StartsWith("OpenSearch.Client", StringComparison.Ordinal)) &&
					InterfaceDataContract.IsDefinedOn(iface))
					return true;
			}

			return false;
		}

		public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
		{
			var converterType = typeof(EmbeddedDomainTypeConverter<>).MakeGenericType(typeToConvert);
			return (JsonConverter)Activator.CreateInstance(converterType, _settings);
		}
	}

	internal sealed class EmbeddedDomainTypeConverter<T> : JsonConverter<T>
	{
		private readonly IConnectionSettingsValues _settings;

		public EmbeddedDomainTypeConverter(IConnectionSettingsValues settings) => _settings = settings;

		private JsonSerializerOptions DomainOptions =>
			_settings.RequestResponseSerializer is IInternalSerializer s && s.TryGetJsonSerializerOptions(out var o)
				? o
				: null;

		public override T Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
		{
			var domain = DomainOptions;
			return domain != null
				? JsonSerializer.Deserialize<T>(ref reader, domain)
				: default;
		}

		public override void Write(Utf8JsonWriter writer, T value, JsonSerializerOptions options)
		{
			var domain = DomainOptions;
			if (domain != null)
				JsonSerializer.Serialize(writer, value, domain);
			else
				writer.WriteNullValue();
		}
	}
}
