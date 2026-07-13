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
using System.Runtime.Serialization;


using OpenSearch.Net.Utf8Json;
namespace OpenSearch.Client
{
	/// <summary>
	/// An attachment indexed with an ingest pipeline using the ingest-attachment plugin.
	/// Convenience class for working with attachment fields.
	/// </summary>
	[JsonFormatter(typeof(AttachmentFormatter))]
	public class Attachment
	{
		/// <summary>
		/// The author
		/// </summary>
		[DataMember(Name = "author")]
		public string Author { get; set; }

		/// <summary>
		/// Whether the attachment contains explicit metadata in addition to the
		/// content. Used at indexing time to determine the serialized form of the
		/// attachment.
		/// </summary>
		[IgnoreDataMember]
		[Ignore]
		public bool ContainsMetadata =>
			!Author.IsNullOrEmpty() ||
			ContentLength.HasValue ||
			!ContentType.IsNullOrEmpty() ||
			Date.HasValue ||
			DetectLanguage.HasValue ||
			IndexedCharacters.HasValue ||
			!Keywords.IsNullOrEmpty() ||
			!Language.IsNullOrEmpty() ||
			!Name.IsNullOrEmpty() ||
			!Title.IsNullOrEmpty();

		/// <summary>
		/// The base64 encoded content. Can be explicitly set
		/// </summary>
		[DataMember(Name = "content")]
		public string Content { get; set; }

		/// <summary>
		/// The length of the content before text extraction.
		/// </summary>
		[DataMember(Name = "content_length")]
		public long? ContentLength { get; set; }

		/// <summary>
		/// The content type of the attachment. Can be explicitly set
		/// </summary>
		[DataMember(Name = "content_type")]
		public string ContentType { get; set; }

		/// <summary>
		/// The date of the attachment.
		/// </summary>
		[DataMember(Name = "date")]
		public DateTime? Date { get; set; }

		/// <summary>
		/// Detect the language of the attachment. Language detection is
		/// disabled by default.
		/// </summary>
		[DataMember(Name = "detect_language")]
		public bool? DetectLanguage { get; set; }

		/// <summary>
		/// Determines how many characters are extracted when indexing the content.
		/// By default, 100000 characters are extracted when indexing the content.
		/// -1 can be set to extract all text, but note that all the text needs to be
		/// allowed to be represented in memory
		/// </summary>
		[DataMember(Name = "indexed_chars")]
		public long? IndexedCharacters { get; set; }

		/// <summary>
		/// The keywords in the attachment.
		/// </summary>
		[DataMember(Name = "keywords")]
		public string Keywords { get; set; }

		/// <summary>
		/// The language of the attachment. Can be explicitly set.
		/// </summary>
		[DataMember(Name = "language")]
		public string Language { get; set; }

		/// <summary>
		/// The name of the attachment. Can be explicitly set
		/// </summary>
		[DataMember(Name = "name")]
		public string Name { get; set; }

		/// <summary>
		/// The title of the attachment.
		/// </summary>
		[DataMember(Name = "title")]
		public string Title { get; set; }
	}

}
