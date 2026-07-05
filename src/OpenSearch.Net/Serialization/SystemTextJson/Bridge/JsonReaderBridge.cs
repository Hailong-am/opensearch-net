// SPDX-License-Identifier: Apache-2.0
//
// The OpenSearch Contributors require contributions made to
// this file be licensed under the Apache-2.0 license or a
// compatible open source license.
//
// Bridge layer: JsonReader backed by STJ Utf8JsonReader.
// Activated via conditional compile symbol USE_STJ_BRIDGE.
// When USE_STJ_BRIDGE is NOT defined, the original Utf8Json/JsonReader.cs is used.
//
// NOTE: Utf8JsonReader is a ref struct -- so this bridge JsonReader must also be a ref struct.
// The original JsonReader is a regular struct. This means any code that stores JsonReader
// in a field (rare -- only in a few test utilities) will need adaptation.
// For the vast majority of usage (pass by ref in IJsonFormatter<T>), ref struct is compatible.

#if USE_STJ_BRIDGE

using System;
using System.Buffers;
using System.Text;
using System.Text.Json;

namespace OpenSearch.Net.Utf8Json
{
	/// <summary>Compat exception class (originally in JsonReader.cs)</summary>
	internal class JsonParsingException : Exception
	{
		public int Offset { get; }
		public string ActualChar { get; set; }

		public JsonParsingException(string message)
			: base(message)
		{
			Offset = 0;
			ActualChar = "";
		}

		public JsonParsingException(string message, byte[] bytes, int offset, int actualOffset, string actual)
			: base(message)
		{
			Offset = offset;
			ActualChar = actual;
		}
	}
	// Note: When USE_STJ_BRIDGE is active, this replaces the original JsonReader.
	// The original Utf8Json/JsonReader.cs must be excluded via conditional compile too.
	internal ref struct JsonReader
	{
		private readonly byte[] _rawBytes;
		private readonly int _initialOffset;
		private Utf8JsonReader _reader;
		private bool _initialized;

		public JsonReader(byte[] bytes) : this(bytes, 0) { }

		public JsonReader(byte[] bytes, int offset)
		{
			_rawBytes = bytes;
			_initialOffset = offset;

			// Skip BOM
			if (bytes.Length >= offset + 3 &&
				bytes[offset] == 0xEF && bytes[offset + 1] == 0xBB && bytes[offset + 2] == 0xBF)
				offset += 3;

			var span = new ReadOnlySpan<byte>(bytes, offset, bytes.Length - offset);
			_reader = new Utf8JsonReader(span, new JsonReaderOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			});
			_initialized = false;
		}

		// --- Position/Buffer access (for DynamicObjectResolver automata) ---

		public byte[] GetBufferUnsafe() => _rawBytes;
		public int GetCurrentOffsetUnsafe() => _initialOffset + (int)_reader.BytesConsumed;

		public void AdvanceOffset(int offset)
		{
			for (int i = 0; i < offset && _reader.Read(); i++) { }
		}

		public void ResetOffset()
		{
			var span = new ReadOnlySpan<byte>(_rawBytes, _initialOffset, _rawBytes.Length - _initialOffset);
			_reader = new Utf8JsonReader(span, new JsonReaderOptions
			{
				AllowTrailingCommas = true,
				CommentHandling = JsonCommentHandling.Skip
			});
			_initialized = false;
		}

		// --- Token inspection ---

		public JsonToken GetCurrentJsonToken()
		{
			EnsureRead();
			return _reader.TokenType switch
			{
				JsonTokenType.StartObject => JsonToken.BeginObject,
				JsonTokenType.EndObject => JsonToken.EndObject,
				JsonTokenType.StartArray => JsonToken.BeginArray,
				JsonTokenType.EndArray => JsonToken.EndArray,
				JsonTokenType.PropertyName => JsonToken.String,
				JsonTokenType.String => JsonToken.String,
				JsonTokenType.Number => JsonToken.Number,
				JsonTokenType.True => JsonToken.True,
				JsonTokenType.False => JsonToken.False,
				JsonTokenType.Null => JsonToken.Null,
				_ => JsonToken.None
			};
		}

		public void SkipWhiteSpace() { /* STJ handles whitespace automatically */ }

		// --- Structural reads ---

		public bool ReadIsNull()
		{
			EnsureRead();
			if (_reader.TokenType == JsonTokenType.Null)
			{
				_reader.Read();
				return true;
			}
			return false;
		}

		public bool ReadIsBeginArray()
		{
			EnsureRead();
			if (_reader.TokenType == JsonTokenType.StartArray)
			{
				_reader.Read();
				return true;
			}
			return false;
		}

		public void ReadIsBeginArrayWithVerify()
		{
			if (!ReadIsBeginArray())
				throw new JsonParsingException("Expected '['", _rawBytes, GetCurrentOffsetUnsafe(), GetCurrentOffsetUnsafe(), "");
		}

		public bool ReadIsEndArray()
		{
			if (_reader.TokenType == JsonTokenType.EndArray)
			{
				_reader.Read();
				return true;
			}
			return false;
		}

		public void ReadIsEndArrayWithVerify()
		{
			if (!ReadIsEndArray())
				throw new JsonParsingException("Expected ']'", _rawBytes, GetCurrentOffsetUnsafe(), GetCurrentOffsetUnsafe(), "");
		}

		public bool ReadIsEndArrayWithSkipValueSeparator(ref int count)
		{
			if (_reader.TokenType == JsonTokenType.EndArray)
			{
				_reader.Read();
				return true;
			}
			count++;
			return false;
		}

		public bool ReadIsInArray(ref int count)
		{
			if (count == 0)
			{
				EnsureRead();
				if (_reader.TokenType != JsonTokenType.StartArray)
					throw new JsonParsingException("Expected '['", _rawBytes, GetCurrentOffsetUnsafe(), GetCurrentOffsetUnsafe(), "");
				_reader.Read();
				if (_reader.TokenType == JsonTokenType.EndArray)
				{
					_reader.Read();
					return false;
				}
				count++;
				return true;
			}
			if (_reader.TokenType == JsonTokenType.EndArray)
			{
				_reader.Read();
				return false;
			}
			count++;
			return true;
		}

		public bool ReadIsBeginObject()
		{
			EnsureRead();
			if (_reader.TokenType == JsonTokenType.StartObject)
			{
				_reader.Read();
				return true;
			}
			return false;
		}

		public void ReadIsBeginObjectWithVerify()
		{
			if (!ReadIsBeginObject())
				throw new JsonParsingException("Expected '{'", _rawBytes, GetCurrentOffsetUnsafe(), GetCurrentOffsetUnsafe(), "");
		}

		public bool ReadIsEndObject()
		{
			if (_reader.TokenType == JsonTokenType.EndObject)
			{
				_reader.Read();
				return true;
			}
			return false;
		}

		public void ReadIsEndObjectWithVerify()
		{
			if (!ReadIsEndObject())
				throw new JsonParsingException("Expected '}'", _rawBytes, GetCurrentOffsetUnsafe(), GetCurrentOffsetUnsafe(), "");
		}

		public bool ReadIsEndObjectWithSkipValueSeparator(ref int count)
		{
			if (_reader.TokenType == JsonTokenType.EndObject)
			{
				_reader.Read();
				return true;
			}
			count++;
			return false;
		}

		public bool ReadIsInObject(ref int count)
		{
			if (count == 0)
			{
				EnsureRead();
				if (_reader.TokenType != JsonTokenType.StartObject)
					throw new JsonParsingException("Expected '{'", _rawBytes, GetCurrentOffsetUnsafe(), GetCurrentOffsetUnsafe(), "");
				_reader.Read();
				if (_reader.TokenType == JsonTokenType.EndObject)
				{
					_reader.Read();
					return false;
				}
				count++;
				return true;
			}
			if (_reader.TokenType == JsonTokenType.EndObject)
			{
				_reader.Read();
				return false;
			}
			count++;
			return true;
		}

		public bool ReadIsValueSeparator() => true; // STJ handles implicitly
		public void ReadIsValueSeparatorWithVerify() { }
		public bool ReadIsNameSeparator() => true;
		public void ReadIsNameSeparatorWithVerify() { }

		// --- Value reads ---

		public string ReadString()
		{
			EnsureRead();
			var value = _reader.GetString();
			_reader.Read();
			return value;
		}

		public string ReadPropertyName()
		{
			if (_reader.TokenType != JsonTokenType.PropertyName)
				throw new JsonParsingException("Expected property name", _rawBytes, GetCurrentOffsetUnsafe(), GetCurrentOffsetUnsafe(), "");
			var name = _reader.GetString();
			_reader.Read();
			return name;
		}

		public ArraySegment<byte> ReadStringSegmentUnsafe()
		{
			EnsureRead();
			var span = _reader.HasValueSequence
				? _reader.ValueSequence.ToArray()
				: _reader.ValueSpan.ToArray();
			_reader.Read();
			return new ArraySegment<byte>(span, 0, span.Length);
		}

		public ArraySegment<byte> ReadStringSegmentRaw() => ReadStringSegmentUnsafe();

		public ArraySegment<byte> ReadPropertyNameSegmentRaw()
		{
			if (_reader.TokenType != JsonTokenType.PropertyName)
				throw new JsonParsingException("Expected property name", _rawBytes, GetCurrentOffsetUnsafe(), GetCurrentOffsetUnsafe(), "");
			var span = _reader.HasValueSequence
				? _reader.ValueSequence.ToArray()
				: _reader.ValueSpan.ToArray();
			_reader.Read();
			return new ArraySegment<byte>(span, 0, span.Length);
		}

		public bool ReadBoolean()
		{
			EnsureRead();
			var value = _reader.GetBoolean();
			_reader.Read();
			return value;
		}

		public sbyte ReadSByte() => checked((sbyte)ReadInt64());
		public short ReadInt16() => checked((short)ReadInt64());
		public int ReadInt32() => checked((int)ReadInt64());

		public long ReadInt64()
		{
			EnsureRead();
			var value = _reader.GetInt64();
			_reader.Read();
			return value;
		}

		public byte ReadByte() => checked((byte)ReadUInt64());
		public ushort ReadUInt16() => checked((ushort)ReadUInt64());
		public uint ReadUInt32() => checked((uint)ReadUInt64());

		public ulong ReadUInt64()
		{
			EnsureRead();
			var value = _reader.GetUInt64();
			_reader.Read();
			return value;
		}

		public float ReadSingle()
		{
			EnsureRead();
			var value = _reader.GetSingle();
			_reader.Read();
			return value;
		}

		public double ReadDouble()
		{
			EnsureRead();
			var value = _reader.GetDouble();
			_reader.Read();
			return value;
		}

		public ArraySegment<byte> ReadNumberSegment()
		{
			EnsureRead();
			var span = _reader.HasValueSequence
				? _reader.ValueSequence.ToArray()
				: _reader.ValueSpan.ToArray();
			_reader.Read();
			return new ArraySegment<byte>(span, 0, span.Length);
		}

		// --- Skip/Next ---

		public void ReadNext() => _reader.Read();

		public void ReadNextBlock()
		{
			if (_reader.TokenType == JsonTokenType.StartObject || _reader.TokenType == JsonTokenType.StartArray)
			{
				_reader.Skip();
				_reader.Read();
			}
			else
			{
				_reader.Read();
			}
		}

		public ArraySegment<byte> ReadNextBlockSegment()
		{
			var start = (int)_reader.BytesConsumed + _initialOffset;
			ReadNextBlock();
			var end = (int)_reader.BytesConsumed + _initialOffset;
			return new ArraySegment<byte>(_rawBytes, start, end - start);
		}

		// --- Internal ---

		private void EnsureRead()
		{
			if (!_initialized)
			{
				_reader.Read();
				_initialized = true;
			}
		}
	}
}

#endif
