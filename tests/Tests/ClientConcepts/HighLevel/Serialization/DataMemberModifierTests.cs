/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/

using System.IO;
using System.Runtime.Serialization;
using System.Text;
using System.Text.Json;
using FluentAssertions;
using OpenSearch.OpenSearch.Xunit.XunitPlumbing;
using OpenSearch.Net;

namespace Tests.ClientConcepts.HighLevel.Serialization
{
	public class DataMemberModifierTests
	{
		/// <summary>
		/// A simple test class with [DataMember(Name = "...")] attributes,
		/// used to verify that the DataMemberModifier correctly applies
		/// the Name value as the JSON property name during serialization.
		/// </summary>
		private class TestDocument
		{
			[DataMember(Name = "custom_name")]
			public string PropertyWithCustomName { get; set; }

			[DataMember(Name = "another_field")]
			public int PropertyWithAnotherName { get; set; }

			/// <summary>
			/// Property without [DataMember(Name)] — should use the default
			/// camelCase naming policy.
			/// </summary>
			public string RegularProperty { get; set; }
		}

		[U]
		public void SerializesUsingDataMemberName()
		{
			var doc = new TestDocument
			{
				PropertyWithCustomName = "hello",
				PropertyWithAnotherName = 42,
				RegularProperty = "world"
			};

			var json = JsonSerializer.Serialize(doc, OpenSearchNetSerializerOptions.Instance);

			json.Should().Contain("\"custom_name\":");
			json.Should().Contain("\"another_field\":");
			json.Should().Contain("\"regularProperty\":");
			json.Should().NotContain("\"propertyWithCustomName\":");
			json.Should().NotContain("\"propertyWithAnotherName\":");
		}

		[U]
		public void DeserializesUsingDataMemberName()
		{
			var json = @"{""custom_name"":""hello"",""another_field"":42,""regularProperty"":""world""}";

			var doc = JsonSerializer.Deserialize<TestDocument>(json, OpenSearchNetSerializerOptions.Instance);

			doc.PropertyWithCustomName.Should().Be("hello");
			doc.PropertyWithAnotherName.Should().Be(42);
			doc.RegularProperty.Should().Be("world");
		}

		[U]
		public void RoundTripPreservesDataMemberNames()
		{
			var original = new TestDocument
			{
				PropertyWithCustomName = "test",
				PropertyWithAnotherName = 7,
				RegularProperty = "value"
			};

			var json = JsonSerializer.Serialize(original, OpenSearchNetSerializerOptions.Instance);
			var deserialized = JsonSerializer.Deserialize<TestDocument>(json, OpenSearchNetSerializerOptions.Instance);

			deserialized.PropertyWithCustomName.Should().Be(original.PropertyWithCustomName);
			deserialized.PropertyWithAnotherName.Should().Be(original.PropertyWithAnotherName);
			deserialized.RegularProperty.Should().Be(original.RegularProperty);
		}

		[U]
		public void NullDataMemberNameDoesNotOverride()
		{
			// [DataMember] without Name should not affect the property name
			var doc = new DocumentWithNullDataMemberName { MyProperty = "value" };

			var json = JsonSerializer.Serialize(doc, OpenSearchNetSerializerOptions.Instance);

			// Should use the camelCase naming policy since DataMember.Name is null
			json.Should().Contain("\"myProperty\":");
		}

		[U]
		public void ModifierWorksWithIndentedOptions()
		{
			var doc = new TestDocument
			{
				PropertyWithCustomName = "hello",
				PropertyWithAnotherName = 42,
				RegularProperty = "world"
			};

			var json = JsonSerializer.Serialize(doc, OpenSearchNetSerializerOptions.Indented);

			json.Should().Contain("\"custom_name\":");
			json.Should().Contain("\"another_field\":");
			json.Should().Contain("\"regularProperty\":");
		}

		private class DocumentWithNullDataMemberName
		{
			[DataMember]
			public string MyProperty { get; set; }
		}
	}
}
