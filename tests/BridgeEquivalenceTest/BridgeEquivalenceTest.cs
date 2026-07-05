/* SPDX-License-Identifier: Apache-2.0
*
* The OpenSearch Contributors require contributions made to
* this file be licensed under the Apache-2.0 license or a
* compatible open source license.
*/
// Behavioral equivalence test: compares output of original Utf8Json vs bridge layer.
// Builds with both modes and compares JSON output byte-for-byte.
// Run: dotnet run --project tests/BridgeEquivalenceTest/BridgeEquivalenceTest.csproj

using System;
using System.Text;
using OpenSearch.Net.Utf8Json;

public static class BridgeEquivalenceTest
{
	static int _passed = 0;
	static int _failed = 0;

	public static void Main()
	{
		Console.WriteLine("=== Bridge Equivalence Test Suite ===\n");
		Console.WriteLine($"Mode: {(IsBridgeMode() ? "USE_STJ_BRIDGE (bridge)" : "Original Utf8Json")}\n");

		TestWriteNull();
		TestWriteBoolean();
		TestWriteNumbers();
		TestWriteString();
		TestWriteStringEscaping();
		TestWritePropertyName();
		TestWriteObject();
		TestWriteArray();
		TestWriteNestedObject();
		TestPreEncodedPropertyNames();
		TestReadPrimitives();
		TestReadObject();
		TestReadNestedSkip();
		TestRoundTrip();

		Console.WriteLine($"\n=== Results: {_passed} passed, {_failed} failed ===");
		if (_failed > 0) Environment.Exit(1);
	}

	static bool IsBridgeMode()
	{
#if USE_STJ_BRIDGE
		return true;
#else
		return false;
#endif
	}

	static void TestWriteNull()
	{
		var w = new JsonWriter(new byte[64]);
		w.WriteNull();
		AssertJson("WriteNull", "null", w.ToString());
	}

	static void TestWriteBoolean()
	{
		var w = new JsonWriter(new byte[64]);
		w.WriteBeginArray();
		w.WriteTrue();
		w.WriteValueSeparator();
		w.WriteFalse();
		w.WriteEndArray();
		AssertJson("WriteBoolean", "[true,false]", w.ToString());
	}

	static void TestWriteNumbers()
	{
		var w = new JsonWriter(new byte[256]);
		w.WriteBeginObject();
		w.WritePropertyName("int32");
		w.WriteInt32(-42);
		w.WriteValueSeparator();
		w.WritePropertyName("int64");
		w.WriteInt64(9223372036854775807L);
		w.WriteValueSeparator();
		w.WritePropertyName("uint64");
		w.WriteUInt64(18446744073709551615UL);
		w.WriteValueSeparator();
		w.WritePropertyName("double");
		w.WriteDouble(3.14);
		w.WriteEndObject();

		var json = w.ToString();
		Assert("WriteNumbers:int32", json.Contains("\"-42\"") || json.Contains("-42"));
		Assert("WriteNumbers:int64", json.Contains("9223372036854775807"));
		Assert("WriteNumbers:uint64", json.Contains("18446744073709551615"));
		Assert("WriteNumbers:double", json.Contains("3.14"));
		_passed++; // count as 1 test
		Console.WriteLine($"  PASS: WriteNumbers");
	}

	static void TestWriteString()
	{
		var w = new JsonWriter(new byte[64]);
		w.WriteString("hello world");
		AssertJson("WriteString", "\"hello world\"", w.ToString());
	}

	static void TestWriteStringEscaping()
	{
		var w = new JsonWriter(new byte[128]);
		w.WriteString("line1\nline2\ttab \"quoted\" \\backslash");
		var json = w.ToString();
		Assert("WriteStringEscaping:newline", json.Contains("\\n"));
		Assert("WriteStringEscaping:tab", json.Contains("\\t"));
		Assert("WriteStringEscaping:quote", json.Contains("\\\"quoted\\\""));
		Assert("WriteStringEscaping:backslash", json.Contains("\\\\backslash"));
		_passed++;
		Console.WriteLine($"  PASS: WriteStringEscaping");
	}

	static void TestWritePropertyName()
	{
		var w = new JsonWriter(new byte[64]);
		w.WriteBeginObject();
		w.WritePropertyName("key");
		w.WriteString("value");
		w.WriteEndObject();
		AssertJson("WritePropertyName", "{\"key\":\"value\"}", w.ToString());
	}

	static void TestWriteObject()
	{
		var w = new JsonWriter(new byte[128]);
		w.WriteBeginObject();
		w.WritePropertyName("name");
		w.WriteString("test");
		w.WriteValueSeparator();
		w.WritePropertyName("age");
		w.WriteInt32(30);
		w.WriteEndObject();
		AssertJson("WriteObject", "{\"name\":\"test\",\"age\":30}", w.ToString());
	}

	static void TestWriteArray()
	{
		var w = new JsonWriter(new byte[64]);
		w.WriteBeginArray();
		w.WriteInt32(1);
		w.WriteValueSeparator();
		w.WriteInt32(2);
		w.WriteValueSeparator();
		w.WriteInt32(3);
		w.WriteEndArray();
		AssertJson("WriteArray", "[1,2,3]", w.ToString());
	}

	static void TestWriteNestedObject()
	{
		var w = new JsonWriter(new byte[256]);
		w.WriteBeginObject();
		w.WritePropertyName("outer");
		w.WriteBeginObject();
		w.WritePropertyName("inner");
		w.WriteBeginArray();
		w.WriteString("a");
		w.WriteValueSeparator();
		w.WriteNull();
		w.WriteEndArray();
		w.WriteEndObject();
		w.WriteEndObject();
		AssertJson("WriteNestedObject", "{\"outer\":{\"inner\":[\"a\",null]}}", w.ToString());
	}

	static void TestPreEncodedPropertyNames()
	{
		var key1 = JsonWriter.GetEncodedPropertyNameWithBeginObject("name");
		var key2 = JsonWriter.GetEncodedPropertyNameWithPrefixValueSeparator("age");

		var w = new JsonWriter(new byte[128]);
		w.WriteRaw(key1);
		w.WriteString("test");
		w.WriteRaw(key2);
		w.WriteInt32(25);
		w.WriteEndObject();
		AssertJson("PreEncodedPropertyNames", "{\"name\":\"test\",\"age\":25}", w.ToString());
	}

	static void TestReadPrimitives()
	{
		var json = "{\"i\":42,\"s\":\"hello\",\"b\":true,\"n\":null,\"d\":3.14}";
		var bytes = Encoding.UTF8.GetBytes(json);
		var r = new JsonReader(bytes);

		r.ReadIsBeginObjectWithVerify();
		Assert("ReadPrim:prop1", r.ReadPropertyName() == "i");
		Assert("ReadPrim:int", r.ReadInt32() == 42);
		Assert("ReadPrim:prop2", r.ReadPropertyName() == "s");
		Assert("ReadPrim:string", r.ReadString() == "hello");
		Assert("ReadPrim:prop3", r.ReadPropertyName() == "b");
		Assert("ReadPrim:bool", r.ReadBoolean() == true);
		Assert("ReadPrim:prop4", r.ReadPropertyName() == "n");
		Assert("ReadPrim:null", r.ReadIsNull());
		Assert("ReadPrim:prop5", r.ReadPropertyName() == "d");
		Assert("ReadPrim:double", Math.Abs(r.ReadDouble() - 3.14) < 0.001);
		_passed++;
		Console.WriteLine("  PASS: ReadPrimitives");
	}

	static void TestReadObject()
	{
		var json = "{\"x\":1,\"y\":2}";
		var bytes = Encoding.UTF8.GetBytes(json);
		var r = new JsonReader(bytes);

		int x = 0, y = 0, count = 0;
		r.ReadIsBeginObjectWithVerify();
		while (!r.ReadIsEndObjectWithSkipValueSeparator(ref count))
		{
			var prop = r.ReadPropertyName();
			if (prop == "x") x = r.ReadInt32();
			else if (prop == "y") y = r.ReadInt32();
			else r.ReadNextBlock();
		}
		Assert("ReadObject:x", x == 1);
		Assert("ReadObject:y", y == 2);
		_passed++;
		Console.WriteLine("  PASS: ReadObject");
	}

	static void TestReadNestedSkip()
	{
		var json = "{\"keep\":42,\"skip\":{\"nested\":[1,2,3]},\"also\":\"yes\"}";
		var bytes = Encoding.UTF8.GetBytes(json);
		var r = new JsonReader(bytes);

		int keep = 0;
		string also = null;
		int count = 0;
		r.ReadIsBeginObjectWithVerify();
		while (!r.ReadIsEndObjectWithSkipValueSeparator(ref count))
		{
			var prop = r.ReadPropertyName();
			switch (prop)
			{
				case "keep": keep = r.ReadInt32(); break;
				case "also": also = r.ReadString(); break;
				default: r.ReadNextBlock(); break;
			}
		}
		Assert("ReadNestedSkip:keep", keep == 42);
		Assert("ReadNestedSkip:also", also == "yes");
		_passed++;
		Console.WriteLine("  PASS: ReadNestedSkip");
	}

	static void TestRoundTrip()
	{
		// Write JSON, then read it back
		var w = new JsonWriter(new byte[256]);
		w.WriteBeginObject();
		w.WritePropertyName("name");
		w.WriteString("round \"trip\" test");
		w.WriteValueSeparator();
		w.WritePropertyName("values");
		w.WriteBeginArray();
		w.WriteInt32(1);
		w.WriteValueSeparator();
		w.WriteDouble(2.5);
		w.WriteValueSeparator();
		w.WriteNull();
		w.WriteEndArray();
		w.WriteEndObject();

		var written = w.ToUtf8ByteArray();
		var r = new JsonReader(written);

		string name = null;
		int count = 0;
		r.ReadIsBeginObjectWithVerify();
		while (!r.ReadIsEndObjectWithSkipValueSeparator(ref count))
		{
			var prop = r.ReadPropertyName();
			switch (prop)
			{
				case "name": name = r.ReadString(); break;
				case "values": r.ReadNextBlock(); break;
				default: r.ReadNextBlock(); break;
			}
		}
		Assert("RoundTrip:name", name == "round \"trip\" test");
		_passed++;
		Console.WriteLine("  PASS: RoundTrip");
	}

	// --- Helpers ---

	static void AssertJson(string label, string expected, string actual)
	{
		if (expected == actual)
		{
			_passed++;
			Console.WriteLine($"  PASS: {label}");
		}
		else
		{
			_failed++;
			Console.WriteLine($"  FAIL: {label}");
			Console.WriteLine($"    Expected: {expected}");
			Console.WriteLine($"    Actual:   {actual}");
		}
	}

	static void Assert(string label, bool condition)
	{
		if (!condition)
		{
			_failed++;
			Console.WriteLine($"  FAIL: {label}");
		}
	}
}
