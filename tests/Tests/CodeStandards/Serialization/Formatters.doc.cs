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
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using FluentAssertions;
using OpenSearch.Client;
using OpenSearch.Net;
using OpenSearch.OpenSearch.Xunit.XunitPlumbing;

namespace Tests.CodeStandards.Serialization
{
	public class Formatters
	{
		[U]
		public void CustomConvertersShouldBeInternal()
		{
			var converters = typeof(IOpenSearchClient).Assembly.GetTypes()
				.Concat(typeof(IOpenSearchLowLevelClient).Assembly.GetTypes())
				.Where(t => !t.IsAbstract && typeof(JsonConverter).IsAssignableFrom(t))
				.ToList();
			var visible = new List<string>();
			foreach (var converter in converters)
			{
				if (converter.IsVisible)
					visible.Add(converter.Name);
			}
			visible.Should().BeEmpty();
		}
	}
}
