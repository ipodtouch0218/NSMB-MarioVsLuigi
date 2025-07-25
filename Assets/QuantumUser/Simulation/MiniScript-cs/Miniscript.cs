using System;
using System.Collections.Generic;

namespace Miniscript {
	public class Error {
		public enum Type {
			Syntax
		}

		public int lineNum;
		public Type type;
		public string description;

		public Error(int lineNum, Type type, string description=null) {
			this.lineNum = lineNum;
			this.type = type;
			if (description == null) {
				this.description = type.ToString();
			} else {
				this.description = description;
			}
		}

		public static void Assert(bool condition) {
			if (!condition) {
				Console.WriteLine("Internal assertion failed.");
			}
		}
	}

	public class Script {
		public List<Error> errors;

		public void Compile(string source) {
		}
	}

}

