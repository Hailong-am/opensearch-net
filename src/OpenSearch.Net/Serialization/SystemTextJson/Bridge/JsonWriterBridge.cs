/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/
//
// Bridge layer: JsonWriter backed by ArrayBufferWriter<byte> + STJ JsonEncodedText.
// Activated via conditional compile symbol USE_STJ_BRIDGE.
// When USE_STJ_BRIDGE is NOT defined, the original Utf8Json/JsonWriter.cs is used.

#if USE_STJ_BRIDGE

using System;
using System.Buffers;
using System.Buffers.Text;
using System.IO;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace OpenSearch.Net.Utf8Json
{
#if NETSTANDARD2_0
	/// <summary>Polyfill: ArrayBufferWriter is internal in System.Memory for netstandard2.0</summary>
	internal sealed class PooledBufferWriter : IBufferWriter<byte>
	{
		private byte[] _buffer;
		private int _written;

		public PooledBufferWriter(int initialCapacity)
		{
			_buffer = new byte[initialCapacity];
			_written = 0;
		}

		public ReadOnlySpan<byte> WrittenSpan => new ReadOnlySpan<byte>(_buffer, 0, _written);
		public int WrittenCount => _written;

		public void Write(ReadOnlySpan<byte> data)
		{
			EnsureCapacity(data.Length);
			data.CopyTo(_buffer.AsSpan(_written));
			_written += data.Length;
		}

		public void Advance(int count) => _written += count;

		public Memory<byte> GetMemory(int sizeHint = 0)
		{
			EnsureCapacity(sizeHint);
			return new Memory<byte>(_buffer, _written, _buffer.Length - _written);
		}

		public Span<byte> GetSpan(int sizeHint = 0)
		{
			EnsureCapacity(sizeHint);
			return new Span<byte>(_buffer, _written, _buffer.Length - _written);
		}

		private void EnsureCapacity(int needed)
		{
			if (_written + needed <= _buffer.Length) return;
			var newSize = Math.Max(_buffer.Length * 2, _written + needed);
			var newBuf = new byte[newSize];
			System.Buffer.BlockCopy(_buffer, 0, newBuf, 0, _written);
			_buffer = newBuf;
		}
	}
#endif

	internal struct JsonWriter
	{
#if NETSTANDARD2_0
		private PooledBufferWriter _bufferWriter;
#else
		private ArrayBufferWriter<byte> _bufferWriter;
#endif

		// Compat: DynamicObjectResolver accesses Buffer/Offset directly (~20 sites).
		// These shims expose the underlying written data. Phase 2 will eliminate direct access.
		internal byte[] Buffer
		{
			get
			{
				// Return a reference to the underlying array (avoids copy for perf-sensitive paths)
				var span = _bufferWriter.WrittenSpan;
				// ArrayBufferWriter doesn't expose its internal array directly.
				// For the bridge POC, copy. Production would use a custom IBufferWriter.
				var arr = new byte[span.Length];
				span.CopyTo(arr);
				return arr;
			}
		}

		internal int Offset => _bufferWriter.WrittenCount;

		public int CurrentOffset => _bufferWriter.WrittenCount;

		public JsonWriter(byte[] initialBuffer)
		{
#if NETSTANDARD2_0
			_bufferWriter = new PooledBufferWriter(initialBuffer?.Length > 0 ? initialBuffer.Length : 256);
#else
			_bufferWriter = new ArrayBufferWriter<byte>(initialBuffer?.Length > 0 ? initialBuffer.Length : 256);
#endif
		}

		public void AdvanceOffset(int offset)
		{
			var span = _bufferWriter.GetSpan(offset);
			_bufferWriter.Advance(offset);
		}

		// --- Static factory methods (return pre-encoded byte[] for caching) ---

		public static byte[] GetEncodedPropertyName(string propertyName)
		{
			var encoded = JsonEncodedText.Encode(propertyName, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
			var bytes = new byte[encoded.EncodedUtf8Bytes.Length + 3]; // "name":
			bytes[0] = (byte)'"';
			encoded.EncodedUtf8Bytes.CopyTo(bytes.AsSpan(1));
			bytes[encoded.EncodedUtf8Bytes.Length + 1] = (byte)'"';
			bytes[encoded.EncodedUtf8Bytes.Length + 2] = (byte)':';
			return bytes;
		}

		public static byte[] GetEncodedPropertyNameWithPrefixValueSeparator(string propertyName)
		{
			var encoded = JsonEncodedText.Encode(propertyName, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
			var bytes = new byte[encoded.EncodedUtf8Bytes.Length + 4]; // ,"name":
			bytes[0] = (byte)',';
			bytes[1] = (byte)'"';
			encoded.EncodedUtf8Bytes.CopyTo(bytes.AsSpan(2));
			bytes[encoded.EncodedUtf8Bytes.Length + 2] = (byte)'"';
			bytes[encoded.EncodedUtf8Bytes.Length + 3] = (byte)':';
			return bytes;
		}

		public static byte[] GetEncodedPropertyNameWithBeginObject(string propertyName)
		{
			var encoded = JsonEncodedText.Encode(propertyName, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
			var bytes = new byte[encoded.EncodedUtf8Bytes.Length + 4]; // {"name":
			bytes[0] = (byte)'{';
			bytes[1] = (byte)'"';
			encoded.EncodedUtf8Bytes.CopyTo(bytes.AsSpan(2));
			bytes[encoded.EncodedUtf8Bytes.Length + 2] = (byte)'"';
			bytes[encoded.EncodedUtf8Bytes.Length + 3] = (byte)':';
			return bytes;
		}

		public static byte[] GetEncodedPropertyNameWithoutQuotation(string propertyName)
		{
			var encoded = JsonEncodedText.Encode(propertyName, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
			return encoded.EncodedUtf8Bytes.ToArray();
		}

		// --- Buffer output ---

		public ArraySegment<byte> GetBuffer()
		{
			var arr = _bufferWriter.WrittenSpan.ToArray();
			return new ArraySegment<byte>(arr, 0, arr.Length);
		}

		public byte[] ToUtf8ByteArray() => _bufferWriter.WrittenSpan.ToArray();

#if NETSTANDARD2_0
		public override string ToString()
		{
			var arr = _bufferWriter.WrittenSpan.ToArray();
			return Encoding.UTF8.GetString(arr, 0, arr.Length);
		}
#else
		public override string ToString() => Encoding.UTF8.GetString(_bufferWriter.WrittenSpan);
#endif

		public void EnsureCapacity(int appendLength)
		{
			_ = _bufferWriter.GetSpan(appendLength);
		}

		// --- Raw byte writes (full WriteRaw compatibility) ---

		public void WriteRaw(byte rawValue)
		{
			var span = _bufferWriter.GetSpan(1);
			span[0] = rawValue;
			_bufferWriter.Advance(1);
		}

		public void WriteRaw(byte[] rawValue)
		{
			if (rawValue == null || rawValue.Length == 0) return;
			_bufferWriter.Write(rawValue);
		}

		public void WriteRaw(byte[] rawValue, int length)
		{
			if (rawValue == null || length == 0) return;
			_bufferWriter.Write(new ReadOnlySpan<byte>(rawValue, 0, length));
		}

		public void WriteRaw(MemoryStream ms)
		{
			if (ms == null || ms.Length == 0) return;
			if (ms.TryGetBuffer(out var segment))
				_bufferWriter.Write(new ReadOnlySpan<byte>(segment.Array!, segment.Offset, segment.Count));
			else
				_bufferWriter.Write(ms.ToArray());
		}

		public void WriteRawUnsafe(byte rawValue)
		{
			var span = _bufferWriter.GetSpan(1);
			span[0] = rawValue;
			_bufferWriter.Advance(1);
		}

		public void WriteSerialized<T>(T value, IOpenSearchSerializer serializer,
			IConnectionConfigurationValues settings,
			SerializationFormatting formatting = SerializationFormatting.None)
		{
			using var ms = settings.MemoryStreamFactory.Create();
			serializer.Serialize(value, ms, formatting);
			WriteRaw(ms);
		}

		// --- Structural tokens ---

		public void WriteBeginObject() => WriteRaw((byte)'{');
		public void WriteEndObject() => WriteRaw((byte)'}');
		public void WriteBeginArray() => WriteRaw((byte)'[');
		public void WriteEndArray() => WriteRaw((byte)']');
		public void WriteValueSeparator() => WriteRaw((byte)',');
		public void WriteNameSeparator() => WriteRaw((byte)':');

		public void WritePropertyName(string propertyName)
		{
			WriteString(propertyName);
			WriteNameSeparator();
		}

		public void WriteQuotation() => WriteRaw((byte)'"');

		// --- Null / Boolean ---

		public void WriteNull()
		{
			ReadOnlySpan<byte> nullBytes = "null"u8;
			_bufferWriter.Write(nullBytes);
		}

		public void WriteBoolean(bool value)
		{
			if (value) WriteTrue(); else WriteFalse();
		}

		public void WriteTrue()
		{
			ReadOnlySpan<byte> trueBytes = "true"u8;
			_bufferWriter.Write(trueBytes);
		}

		public void WriteFalse()
		{
			ReadOnlySpan<byte> falseBytes = "false"u8;
			_bufferWriter.Write(falseBytes);
		}

		// --- Numbers (stackalloc + Utf8Formatter -- zero heap allocation) ---

		public void WriteSingle(float value)
		{
			Span<byte> tmp = stackalloc byte[32];
			Utf8Formatter.TryFormat(value, tmp, out int written);
			_bufferWriter.Write(tmp.Slice(0, written));
		}

		public void WriteDouble(double value)
		{
			Span<byte> tmp = stackalloc byte[32];
			Utf8Formatter.TryFormat(value, tmp, out int written);
			_bufferWriter.Write(tmp.Slice(0, written));
		}

		public void WriteByte(byte value) => WriteUInt64(value);
		public void WriteUInt16(ushort value) => WriteUInt64(value);
		public void WriteUInt32(uint value) => WriteUInt64(value);

		public void WriteUInt64(ulong value)
		{
			Span<byte> tmp = stackalloc byte[20];
			Utf8Formatter.TryFormat(value, tmp, out int written);
			_bufferWriter.Write(tmp.Slice(0, written));
		}

		public void WriteSByte(sbyte value) => WriteInt64(value);
		public void WriteInt16(short value) => WriteInt64(value);
		public void WriteInt32(int value) => WriteInt64(value);

		public void WriteInt64(long value)
		{
			Span<byte> tmp = stackalloc byte[20];
			Utf8Formatter.TryFormat(value, tmp, out int written);
			_bufferWriter.Write(tmp.Slice(0, written));
		}

		// --- String (uses STJ JsonEncodedText for correct escaping) ---

		public void WriteString(string value)
		{
			if (value == null) { WriteNull(); return; }

			WriteRaw((byte)'"');
			var encoded = JsonEncodedText.Encode(value, JavaScriptEncoder.UnsafeRelaxedJsonEscaping);
			_bufferWriter.Write(encoded.EncodedUtf8Bytes);
			WriteRaw((byte)'"');
		}

		// --- Compat: ToUnicode for control chars (used by original WriteString) ---
		// Not needed in bridge -- JsonEncodedText handles all escaping.
	}
}

#endif
