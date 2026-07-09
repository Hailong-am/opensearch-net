/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/
/*
* Modifications Copyright OpenSearch Contributors. See
* GitHub history for details.
*
*  Licensed to Elasticsearch B.V. under one or more contributor
*  license agreements. See the NOTICE file distributed with
*  this work for additional information regarding copyright
*  ownership. Elasticsearch B.V. licenses this file to you under
*  the Apache License, Version 2.0 (the "License"); you may
*  not use this file except in compliance with the License.
*  You may obtain a copy of the License at
*
* 	http://www.apache.org/licenses/LICENSE-2.0
*
*  Unless required by applicable law or agreed to in writing,
*  software distributed under the License is distributed on an
*  "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
*  KIND, either express or implied.  See the License for the
*  specific language governing permissions and limitations
*  under the License.
*/

using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenSearch.Net
{
	/// <summary>
	/// Marks an enum (or a property) as serializing to its <see cref="System.Runtime.Serialization.EnumMemberAttribute"/>
	/// string form. By inheriting <see cref="JsonConverterAttribute"/>, the enum-member converter is attached via
	/// the System.Text.Json attribute/contract channel, so it is honored in every nesting context — including
	/// collection elements and union members — where an options-registered converter factory would be skipped.
	/// </summary>
	[AttributeUsage(AttributeTargets.Property | AttributeTargets.Enum)]
	public class StringEnumAttribute : JsonConverterAttribute
	{
		private static readonly EnumMemberConverterFactory Factory = new EnumMemberConverterFactory();

		public override JsonConverter CreateConverter(Type typeToConvert) =>
			Factory.CanConvert(typeToConvert)
				? Factory.CreateConverter(typeToConvert, JsonSerializerOptions.Default)
				: null;
	}
}
