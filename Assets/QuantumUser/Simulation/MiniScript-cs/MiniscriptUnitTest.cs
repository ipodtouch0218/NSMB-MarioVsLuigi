/*	MiniscriptUnitTest.cs

This file contains a number of unit tests for various parts of the MiniScript
architecture.  It's used by the MiniScript developers to ensure we don't
break something when we make changes.

You can safely ignore this, but if you really want to run the tests yourself,
just call Miniscript.UnitTest.Run().

*/
using System;

namespace Miniscript {
	public static class UnitTest {
		public static void ReportError(string err) {
			// Set a breakpoint here if you want to drop into the debugger
			// on any unit test failure.
			Console.WriteLine(err);
			#if UNITY_EDITOR
			UnityEngine.Debug.LogError("Miniscript unit test failed: " + err);
			#endif
		}

		public static void ErrorIf(bool condition, string err) {
			if (condition) ReportError(err);
		}

		public static void ErrorIfNull(object obj) {
			if (obj == null) ReportError("Unexpected null");
		}

		public static void ErrorIfNotNull(object obj) { 
			if (obj != null) ReportError("Expected null, but got non-null");
		}

		public static void ErrorIfNotEqual(string actual, string expected,
			string desc="Expected {1}, got {0}") {
			if (actual == expected) return;
			ReportError(string.Format(desc, actual, expected));
		}

		public static void ErrorIfNotEqual(float actual, float expected,
			string desc="Expected {1}, got {0}") {
			if (actual == expected) return;
			ReportError(string.Format(desc, actual, expected));
		}

		public static void Run() {
			Lexer.RunUnitTests();
			Parser.RunUnitTests();
		}
	}
}

