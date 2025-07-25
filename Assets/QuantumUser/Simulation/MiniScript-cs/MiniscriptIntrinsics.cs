/*	MiniscriptIntrinsics.cs

This file defines the Intrinsic class, which represents a built-in function
available to MiniScript code.  All intrinsics are held in static storage, so
this class includes static functions such as GetByName to look up 
already-defined intrinsics.  See Chapter 2 of the MiniScript Integration
Guide for details on adding your own intrinsics.

This file also contains the Intrinsics static class, where all of the standard
intrinsics are defined.  This is initialized automatically, so normally you
don’t need to worry about it, though it is a good place to look for examples
of how to write intrinsic functions.

Note that you should put any intrinsics you add in a separate file; leave the
MiniScript source files untouched, so you can easily replace them when updates
become available.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Photon.Deterministic;
using Quantum;

namespace Miniscript {
	/// <summary>
	/// IntrinsicCode is a delegate to the actual C# code invoked by an intrinsic method.
	/// </summary>
	/// <param name="context">TAC.Context in which the intrinsic was invoked</param>
	/// <param name="partialResult">partial result from a previous invocation, if any</param>
	/// <returns>result of the computation: whether it's complete, a partial result if not, and a Value if so</returns>
	public delegate Intrinsic.Result IntrinsicCode(TAC.Context context, Intrinsic.Result partialResult);
	
	/// <summary>
	/// Information about the app hosting MiniScript.  Set this in your main program.
	/// This is provided to the user via the `version` intrinsic.
	/// </summary>
	public static class HostInfo {
		public static string name;		// name of the host program
		public static string info;		// URL or other short info about the host
		public static double version;	// host program version number
	}
		
	/// <summary>
	/// Intrinsic: represents an intrinsic function available to MiniScript code.
	/// </summary>
	public class Intrinsic {
		// name of this intrinsic (should be a valid MiniScript identifier)
		public string name;
		
		// actual C# code invoked by the intrinsic
		public IntrinsicCode code;
		
		// a numeric ID (used internally -- don't worry about this)
		public int id { get { return numericID; } }

		// static map from Values to short names, used when displaying lists/maps;
		// feel free to add to this any values (especially lists/maps) provided
		// by your own intrinsics.
		public static Dictionary<Value, string> shortNames = new Dictionary<Value, string>();

		private Function function;
		private ValFunction valFunction;	// (cached wrapper for function)
		int numericID;		// also its index in the 'all' list

		public static List<Intrinsic> all = new List<Intrinsic>() { null };
		static Dictionary<string, Intrinsic> nameMap = new Dictionary<string, Intrinsic>();
				
		/// <summary>
		/// Factory method to create a new Intrinsic, filling out its name as given,
		/// and other internal properties as needed.  You'll still need to add any
		/// parameters, and define the code it runs.
		/// </summary>
		/// <param name="name">intrinsic name</param>
		/// <returns>freshly minted (but empty) static Intrinsic</returns>
		public static Intrinsic Create(string name) {
			Intrinsic result = new Intrinsic();
			result.name = name;
			result.numericID = all.Count;
			result.function = new Function(null);
			result.valFunction = new ValFunction(result.function);
			all.Add(result);
			nameMap[name] = result;
			return result;
		}
		
		/// <summary>
		/// Look up an Intrinsic by its internal numeric ID.
		/// </summary>
		public static Intrinsic GetByID(int id) {
			return all[id];
		}
		
		/// <summary>
		/// Look up an Intrinsic by its name.
		/// </summary>
		public static Intrinsic GetByName(string name) {
			Intrinsics.InitIfNeeded();
			Intrinsic result = null;
			if (nameMap.TryGetValue(name, out result)) return result;
			return null;
		}
		
		/// <summary>
		/// Add a parameter to this Intrinsic, optionally with a default value
		/// to be used if the user doesn't supply one.  You must add parameters
		/// in the same order in which arguments must be supplied.
		/// </summary>
		/// <param name="name">parameter name</param>
		/// <param name="defaultValue">default value, if any</param>
		public void AddParam(string name, Value defaultValue=null) {
			function.parameters.Add(new Function.Param(name, defaultValue));
		}
		
		/// <summary>
		/// Add a parameter with a numeric default value.  (See comments on
		/// the first version of AddParam above.)
		/// </summary>
		/// <param name="name">parameter name</param>
		/// <param name="defaultValue">default value for this parameter</param>
		public void AddParam(string name, FP defaultValue) {
			Value defVal;
			if (defaultValue == 0) defVal = ValNumber.zero;
			else if (defaultValue == 1) defVal = ValNumber.one;
			else defVal = TAC.Num(defaultValue);
			function.parameters.Add(new Function.Param(name, defVal));
		}

		/// <summary>
		/// Add a parameter with a string default value.  (See comments on
		/// the first version of AddParam above.)
		/// </summary>
		/// <param name="name">parameter name</param>
		/// <param name="defaultValue">default value for this parameter</param>
		public void AddParam(string name, string defaultValue) {
			Value defVal;
			if (string.IsNullOrEmpty(defaultValue)) defVal = ValString.empty;
			else if (defaultValue == "__isa") defVal = ValString.magicIsA;
			else if (defaultValue == "self") defVal = _self;
			else defVal = new ValString(defaultValue);
			function.parameters.Add(new Function.Param(name, defVal));
		}
		ValString _self = new ValString("self");
		
		/// <summary>
		/// GetFunc is used internally by the compiler to get the MiniScript function
		/// that makes an intrinsic call.
		/// </summary>
		public ValFunction GetFunc() {
			if (function.code == null) {
				// Our little wrapper function is a single opcode: CallIntrinsicA.
				// It really exists only to provide a local variable context for the parameters.
				function.code = new List<TAC.Line>();
				function.code.Add(new TAC.Line(TAC.LTemp(0), TAC.Line.Op.CallIntrinsicA, TAC.Num(numericID)));
			}
			return valFunction;
		}
		
		/// <summary>
		/// Internally-used function to execute an intrinsic (by ID) given a
		/// context and a partial result.
		/// </summary>
		public static Result Execute(int id, TAC.Context context, Result partialResult) {
			Intrinsic item = GetByID(id);
			return item.code(context, partialResult);
		}
		
		/// <summary>
		/// Result represents the result of an intrinsic call.  An intrinsic will either
		/// be done with its work, or not yet done (e.g. because it's waiting for something).
		/// If it's done, set done=true, and store the result Value in result.
		/// If it's not done, set done=false, and store any partial result in result (and 
		/// then your intrinsic will get invoked with this Result passed in as partialResult).
		/// </summary>
		public class Result {
			public bool done;		// true if our work is complete; false if we need to Continue
			public Value result;	// final result if done; in-progress data if not done
			
			/// <summary>
			/// Result constructor taking a Value, and an optional done flag.
			/// </summary>
			/// <param name="result">result or partial result of the call</param>
			/// <param name="done">whether our work is done (optional, defaults to true)</param>
			public Result(Value result, bool done=true) {
				this.done = done;
				this.result = result;
			}

			/// <summary>
			/// Result constructor for a simple numeric result.
			/// </summary>
			public Result(FP resultNum) {
				this.done = true;
				this.result = new ValNumber(resultNum);
			}

			/// <summary>
			/// Result constructor for a simple string result.
			/// </summary>
			public Result(string resultStr) {
				this.done = true;
				if (string.IsNullOrEmpty(resultStr)) this.result = ValString.empty;
				else this.result = new ValString(resultStr);
			}
			
			/// <summary>
			/// Result.Null: static Result representing null (no value).
			/// </summary>
			public static Result Null { get { return _null; } }
			static Result _null = new Result(null, true);
			
			/// <summary>
			/// Result.EmptyString: static Result representing "" (empty string).
			/// </summary>
			public static Result EmptyString { get { return _emptyString; } }
			static Result _emptyString = new Result(ValString.empty);
			
			/// <summary>
			/// Result.True: static Result representing true (1.0).
			/// </summary>
			public static Result True { get { return _true; } }
			static Result _true = new Result(ValNumber.one, true);
			
			/// <summary>
			/// Result.True: static Result representing false (0.0).
			/// </summary>
			public static Result False { get { return _false; } }
			static Result _false = new Result(ValNumber.zero, true);
			
			/// <summary>
			/// Result.Waiting: static Result representing a need to wait,
			/// with no in-progress value.
			/// </summary>
			public static Result Waiting { get { return _waiting; } }
			static Result _waiting = new Result(null, false);
		}
	}
	
	/// <summary>
	/// Intrinsics: a static class containing all of the standard MiniScript
	/// built-in intrinsics.  You shouldn't muck with these, but feel free
	/// to browse them for lots of examples of how to write your own intrinics.
	/// </summary>
	public static class Intrinsics {

		static bool initialized;
		static ValMap intrinsicsMap = null;		// (for "intrinsics" function)

		private struct KeyedValue {
			public Value sortKey;
			public Value value;
			//public long valueIndex;
		}
		
		// Helper method to get a stack trace, as a list of ValStrings.
		// This is the heart of the stackTrace intrinsic.
		// Public in case you want to call it from other places (for debugging, etc.).
		public static ValList StackList(TAC.Machine vm) {
			ValList result = new ValList();
			if (vm == null) return result;
			foreach (SourceLoc loc in vm.GetStack()) {
				if (loc == null) continue;
				string s = loc.context;
				if (string.IsNullOrEmpty(s)) s = "(current program)";
				s += " line " + loc.lineNum;
				result.values.Add(new ValString(s));
			}
			return result;
		}


		/// <summary>
		/// InitIfNeeded: called automatically during script setup to make sure
		/// that all our standard intrinsics are defined.  Note how we use a
		/// private bool flag to ensure that we don't create our intrinsics more
		/// than once, no matter how many times this method is called.
		/// </summary>
		public static unsafe void InitIfNeeded() {
			if (initialized) return;	// our work is already done; bail out.
			initialized = true;
			Intrinsic f;

			// abs
			//	Returns the absolute value of the given number.
			// x (number, default 0): number to take the absolute value of.
			// Example: abs(-42)		returns 42
			f = Intrinsic.Create("abs");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Abs(context.GetLocalDouble("x")));
			};

			// acos
			//	Returns the inverse cosine, that is, the angle 
			//	(in radians) whose cosine is the given value.
			// x (number, default 0): cosine of the angle to find.
			// Returns: angle, in radians, whose cosine is x.
			// Example: acos(0) 		returns 1.570796
			f = Intrinsic.Create("acos");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Acos(context.GetLocalDouble("x")));
			};

			// asin
			//	Returns the inverse sine, that is, the angle
			//	(in radians) whose sine is the given value.
			// x (number, default 0): cosine of the angle to find.
			// Returns: angle, in radians, whose cosine is x.
			// Example: asin(1) return 1.570796
			f = Intrinsic.Create("asin");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Asin(context.GetLocalDouble("x")));
			};

			// atan
			//	Returns the arctangent of a value or ratio, that is, the
			//	angle (in radians) whose tangent is y/x.  This will return
			//	an angle in the correct quadrant, taking into account the
			//	sign of both arguments.  The second argument is optional,
			//	and if omitted, this function is equivalent to the traditional
			//	one-parameter atan function.  Note that the parameters are
			//	in y,x order.
			// y (number, default 0): height of the side opposite the angle
			// x (number, default 1): length of the side adjacent the angle
			// Returns: angle, in radians, whose tangent is y/x
			// Example: atan(1, -1)		returns 2.356194
			f = Intrinsic.Create("atan");
			f.AddParam("y", 0);
			f.AddParam("x", 1);
			f.code = (context, partialResult) => {
				FP y = context.GetLocalDouble("y");
				FP x = context.GetLocalDouble("x");
				if (x == 1) return new Intrinsic.Result(FPMath.Atan(y));
				return new Intrinsic.Result(FPMath.Atan2(y, x));
			};

			Func<FP, Tuple<bool, ulong>> doubleToUnsignedSplit = (val) => {
				return new Tuple<bool, ulong>(FPMath.Sign(val) == -1, (ulong) FPMath.Abs(val));
			};

			// bitAnd
			//	Treats its arguments as integers, and computes the bitwise
			//	`and`: each bit in the result is set only if the corresponding
			//	bit is set in both arguments.
			// i (number, default 0): first integer argument
			// j (number, default 0): second integer argument
			// Returns: bitwise `and` of i and j
			// Example: bitAnd(14, 7)		returns 6
			// See also: bitOr; bitXor
			f = Intrinsic.Create("bitAnd");
			f.AddParam("i", 0);
			f.AddParam("j", 0);
			f.code = (context, partialResult) => {
				var i = doubleToUnsignedSplit(context.GetLocalDouble("i"));
				var j = doubleToUnsignedSplit(context.GetLocalDouble("j"));
				var sign = i.Item1 & j.Item1;
				FP val = (int) (i.Item2 & j.Item2);
				return new Intrinsic.Result(sign ? -val : val);
            };

			// bitOr
			//	Treats its arguments as integers, and computes the bitwise
			//	`or`: each bit in the result is set if the corresponding
			//	bit is set in either (or both) of the arguments.
			// i (number, default 0): first integer argument
			// j (number, default 0): second integer argument
			// Returns: bitwise `or` of i and j
			// Example: bitOr(14, 7)		returns 15
			// See also: bitAnd; bitXor
			f = Intrinsic.Create("bitOr");
			f.AddParam("i", 0);
			f.AddParam("j", 0);
			f.code = (context, partialResult) => {
				var i = doubleToUnsignedSplit(context.GetLocalDouble("i"));
				var j = doubleToUnsignedSplit(context.GetLocalDouble("j"));
				var sign = i.Item1 | j.Item1;
				FP val = (int) (i.Item2 | j.Item2);
                return new Intrinsic.Result(sign ? -val : val);
			};
			
			// bitXor
			//	Treats its arguments as integers, and computes the bitwise
			//	`xor`: each bit in the result is set only if the corresponding
			//	bit is set in exactly one (not zero or both) of the arguments.
			// i (number, default 0): first integer argument
			// j (number, default 0): second integer argument
			// Returns: bitwise `xor` of i and j
			// Example: bitXor(14, 7)		returns 9
			// See also: bitAnd; bitOr
			f = Intrinsic.Create("bitXor");
			f.AddParam("i", 0);
			f.AddParam("j", 0);
			f.code = (context, partialResult) => {
                var i = doubleToUnsignedSplit(context.GetLocalDouble("i"));
                var j = doubleToUnsignedSplit(context.GetLocalDouble("j"));
                var sign = i.Item1 ^ j.Item1;
                FP val = (int) (i.Item2 ^ j.Item2);
                return new Intrinsic.Result(sign ? -val : val);
            };
			
			// char
			//	Gets a character from its Unicode code point.
			// codePoint (number, default 65): Unicode code point of a character
			// Returns: string containing the specified character
			// Example: char(42)		returns "*"
			// See also: code
			f = Intrinsic.Create("char");
			f.AddParam("codePoint", 65);
			f.code = (context, partialResult) => {
				int codepoint = context.GetLocalInt("codePoint");
				string s = char.ConvertFromUtf32(codepoint);
				return new Intrinsic.Result(s);
			};
			
			// ceil
			//	Returns the "ceiling", i.e. closest whole number 
			//	greater than or equal to the given number.
			// x (number, default 0): number to get the ceiling of
			// Returns: closest whole number not less than x
			// Example: ceil(41.2)		returns 42
			// See also: floor
			f = Intrinsic.Create("ceil");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Ceiling(context.GetLocalDouble("x")));
			};
			
			// code
			//	Return the Unicode code point of the first character of
			//	the given string.  This is the inverse of `char`.
			//	May be called with function syntax or dot syntax.
			// self (string): string to get the code point of
			// Returns: Unicode code point of the first character of self
			// Example: "*".code		returns 42
			// Example: code("*")		returns 42
			f = Intrinsic.Create("code");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.self;
				int codepoint = 0;
				if (self != null) codepoint = char.ConvertToUtf32(self.ToString(), 0);
				return new Intrinsic.Result(codepoint);
			};
						
			// cos
			//	Returns the cosine of the given angle (in radians).
			// radians (number): angle, in radians, to get the cosine of
			// Returns: cosine of the given angle
			// Example: cos(0)		returns 1
			f = Intrinsic.Create("cos");
			f.AddParam("radians", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Cos(context.GetLocalDouble("radians")));
			};

			// floor
			//	Returns the "floor", i.e. closest whole number 
			//	less than or equal to the given number.
			// x (number, default 0): number to get the floor of
			// Returns: closest whole number not more than x
			// Example: floor(42.9)		returns 42
			// See also: floor
			f = Intrinsic.Create("floor");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Floor(context.GetLocalDouble("x")));
			};

			// funcRef
			//	Returns a map that represents a function reference in
			//	MiniScript's core type system.  This can be used with `isa`
			//	to check whether a variable refers to a function (but be
			//	sure to use @ to avoid invoking the function and testing
			//	the result).
			// Example: @floor isa funcRef		returns 1
			// See also: number, string, list, map
			f = Intrinsic.Create("funcRef");
			f.code = (context, partialResult) => {
				if (context.vm.functionType == null) {
					context.vm.functionType = FunctionType().EvalCopy(context.vm.globalContext);
				}
				return new Intrinsic.Result(context.vm.functionType);
			};
			
			// hash
			//	Returns an integer that is "relatively unique" to the given value.
			//	In the case of strings, the hash is case-sensitive.  In the case
			//	of a list or map, the hash combines the hash values of all elements.
			//	Note that the value returned is platform-dependent, and may vary
			//	across different MiniScript implementations.
			// obj (any type): value to hash
			// Returns: integer hash of the given value
			f = Intrinsic.Create("hash");
			f.AddParam("obj");
			f.code = (context, partialResult) => {
				Value val = context.GetLocal("obj");
				return new Intrinsic.Result(val.Hash());
			};

			// hasIndex
			//	Return whether the given index is valid for this object, that is,
			//	whether it could be used with square brackets to get some value
			//	from self.  When self is a list or string, the result is true for
			//	integers from -(length of string) to (length of string-1).  When
			//	self is a map, it is true for any key (index) in the map.  If
			//	called on a number, this method throws a runtime exception.
			// self (string, list, or map): object to check for an index on
			// index (any): value to consider as a possible index
			// Returns: 1 if self[index] would be valid; 0 otherwise
			// Example: "foo".hasIndex(2)		returns 1
			// Example: "foo".hasIndex(3)		returns 0
			// See also: indexes
			f = Intrinsic.Create("hasIndex");
			f.AddParam("self");
			f.AddParam("index");
			f.code = (context, partialResult) => {
				Value self = context.self;
				Value index = context.GetLocal("index");
				if (self is ValList) {
					if (index is ValNumber) {
						List<Value> list = ((ValList)self).values;
						int i = index.IntValue();
						return new Intrinsic.Result(ValNumber.Truth(i >= -list.Count && i < list.Count));
					}
					return Intrinsic.Result.False;
				} else if (self is ValString) {
					if (index is ValNumber) {
						string str = ((ValString)self).value;
						int i = index.IntValue();
						return new Intrinsic.Result(ValNumber.Truth(i >= -str.Length && i < str.Length));
					}
					return new Intrinsic.Result(ValNumber.zero);
				} else if (self is ValMap) {
					ValMap map = (ValMap)self;
					return new Intrinsic.Result(ValNumber.Truth(map.ContainsKey(index)));
				}
				return Intrinsic.Result.Null;
			};
			
			// indexes
			//	Returns the keys of a dictionary, or the non-negative indexes
			//	for a string or list.
			// self (string, list, or map): object to get the indexes of
			// Returns: a list of valid indexes for self
			// Example: "foo".indexes		returns [0, 1, 2]
			// See also: hasIndex
			f = Intrinsic.Create("indexes");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.self;
				if (self is ValMap) {
					ValMap map = (ValMap)self;
					List<Value> keys = new List<Value>(map.map.Keys);
					for (int i = 0; i < keys.Count; i++) if (keys[i] is ValNull) keys[i] = null;
					return new Intrinsic.Result(new ValList(keys));
				} else if (self is ValString) {
					string str = ((ValString)self).value;
					List<Value> indexes = new List<Value>(str.Length);
					for (int i = 0; i < str.Length; i++) {
						indexes.Add(TAC.Num(i));
					}
					return new Intrinsic.Result(new ValList(indexes));
				} else if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					List<Value> indexes = new List<Value>(list.Count);
					for (int i = 0; i < list.Count; i++) {
						indexes.Add(TAC.Num(i));
					}
					return new Intrinsic.Result(new ValList(indexes));
				}
				return Intrinsic.Result.Null;
			};
			
			// indexOf
			//	Returns index or key of the given value, or if not found,		returns null.
			// self (string, list, or map): object to search
			// value (any): value to search for
			// after (any, optional): if given, starts the search after this index
			// Returns: first index (after `after`) such that self[index] == value, or null
			// Example: "Hello World".indexOf("o")		returns 4
			// Example: "Hello World".indexOf("o", 4)		returns 7
			// Example: "Hello World".indexOf("o", 7)		returns null			
			f = Intrinsic.Create("indexOf");
			f.AddParam("self");
			f.AddParam("value");
			f.AddParam("after");
			f.code = (context, partialResult) => {
				Value self = context.self;
				Value value = context.GetLocal("value");
				Value after = context.GetLocal("after");
				if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					int idx;
					if (after == null) idx = list.FindIndex(x => 
						x == null ? value == null : x.Equality(value) == 1);
					else {
						int afterIdx = after.IntValue();
						if (afterIdx < -1) afterIdx += list.Count;
						if (afterIdx < -1 || afterIdx >= list.Count-1) return Intrinsic.Result.Null;
						idx = list.FindIndex(afterIdx + 1, x => 
							x == null ? value == null : x.Equality(value) == 1);
					}
					if (idx >= 0) return new Intrinsic.Result(idx);
				} else if (self is ValString) {
					string str = ((ValString)self).value;
					if (value == null) return Intrinsic.Result.Null;
					string s = value.ToString();
					int idx;
					if (after == null) idx = str.IndexOf(s, StringComparison.Ordinal);
					else {
						int afterIdx = after.IntValue();
						if (afterIdx < -1) afterIdx += str.Length;
						if (afterIdx < -1 || afterIdx >= str.Length-1) return Intrinsic.Result.Null;
						idx = str.IndexOf(s, afterIdx + 1, StringComparison.Ordinal);
					}
					if (idx >= 0) return new Intrinsic.Result(idx);
				} else if (self is ValMap) {
					ValMap map = (ValMap)self;
					bool sawAfter = (after == null);
					foreach (var kv in map.map) {
						if (!sawAfter) {
							if (kv.Key.Equality(after) == 1) sawAfter = true;
						} else {
							if (kv.Value == null ? value == null : kv.Value.Equality(value) == 1) return new Intrinsic.Result(kv.Key);
						}
					}
				}
				return Intrinsic.Result.Null;
			};

			// insert
			//	Insert a new element into a string or list.  In the case of a list,
			//	the list is both modified in place and returned.  Strings are immutable,
			//	so in that case the original string is unchanged, but a new string is
			//	returned with the value inserted.
			// self (string or list): sequence to insert into
			// index (number): position at which to insert the new item
			// value (any): element to insert at the specified index
			// Returns: modified list, new string
			// Example: "Hello".insert(2, 42)		returns "He42llo"
			// See also: remove
			f = Intrinsic.Create("insert");
			f.AddParam("self");
			f.AddParam("index");
			f.AddParam("value");
			f.code = (context, partialResult) => {
				Value self = context.self;
				Value index = context.GetLocal("index");
				Value value = context.GetLocal("value");
				if (index == null) throw new RuntimeException("insert: index argument required");
				if (!(index is ValNumber)) throw new RuntimeException("insert: number required for index argument");
				int idx = index.IntValue();
				if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					if (idx < 0) idx += list.Count + 1;	// +1 because we are inserting AND counting from the end.
					Check.Range(idx, 0, list.Count);	// and allowing all the way up to .Count here, because insert.
					list.Insert(idx, value);
					return new Intrinsic.Result(self);
				} else if (self is ValString) {
					string s = self.ToString();
					if (idx < 0) idx += s.Length + 1;
					Check.Range(idx, 0, s.Length);
					s = s.Substring(0, idx) + value.ToString() + s.Substring(idx);
					return new Intrinsic.Result(s);
				} else {
					throw new RuntimeException("insert called on invalid type");
				}
			};

			// intrinsics
			//	Returns a read-only map of all named intrinsics.
			f = Intrinsic.Create("intrinsics");
			f.code = (context, partialResult) => {
				if (intrinsicsMap != null) return new Intrinsic.Result(intrinsicsMap);
				intrinsicsMap = new ValMap();
				intrinsicsMap.assignOverride = (s,k,v) => {
					throw new RuntimeException("Assignment to protected map");
					return true;
				};
		
				foreach (var intrinsic in Intrinsic.all) {
					if (intrinsic == null || string.IsNullOrEmpty(intrinsic.name)) continue;
					intrinsicsMap[intrinsic.name] = intrinsic.GetFunc();
				}
		
				return new Intrinsic.Result(intrinsicsMap);
			};

			// self.join
			//	Join the elements of a list together to form a string.
			// self (list): list to join
			// delimiter (string, default " "): string to insert between each pair of elements
			// Returns: string built by joining elements of self with delimiter
			// Example: [2,4,8].join("-")		returns "2-4-8"
			// See also: split
			f = Intrinsic.Create("join");
			f.AddParam("self");
			f.AddParam("delimiter", " ");
			f.code = (context, partialResult) => {
				Value val = context.self;
				string delim = context.GetLocalString("delimiter");
				if (!(val is ValList)) return new Intrinsic.Result(val);
				ValList src = (val as ValList);
				List<string> list = new List<string>(src.values.Count);
				for (int i=0; i<src.values.Count; i++) {
					if (src.values[i] == null) list.Add(null);
					else list.Add(src.values[i].ToString());
				}
				string result = string.Join(delim, list.ToArray());
				return new Intrinsic.Result(result);
			};
			
			// self.len
			//	Return the number of characters in a string, elements in
			//	a list, or key/value pairs in a map.
			//	May be called with function syntax or dot syntax.
			// self (list, string, or map): object to get the length of
			// Returns: length (number of elements) in self
			// Example: "hello".len		returns 5
			f = Intrinsic.Create("len");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value val = context.self;
				if (val is ValList) {
					List<Value> list = ((ValList)val).values;
					return new Intrinsic.Result(list.Count);
				} else if (val is ValString) {
					string str = ((ValString)val).value;
					return new Intrinsic.Result(str.Length);
				} else if (val is ValMap) {
					return new Intrinsic.Result(((ValMap)val).Count);
				}
				return Intrinsic.Result.Null;
			};
			
			// list type
			//	Returns a map that represents the list datatype in
			//	MiniScript's core type system.  This can be used with `isa`
			//	to check whether a variable refers to a list.  You can also
			//	assign new methods here to make them available to all lists.
			// Example: [1, 2, 3] isa list		returns 1
			// See also: number, string, map, funcRef
			f = Intrinsic.Create("list");
			f.code = (context, partialResult) => {
				if (context.vm.listType == null) {
					context.vm.listType = ListType().EvalCopy(context.vm.globalContext);
				}
				return new Intrinsic.Result(context.vm.listType);
			};
			
			// log(x, base)
			//	Returns the logarithm (with the given) of the given number,
			//	that is, the number y such that base^y = x.
			// x (number): number to take the log of
			// base (number, default 10): logarithm base
			// Returns: a number that, when base is raised to it, produces x
			// Example: log(1000)		returns 3 (because 10^3 == 1000)
			f = Intrinsic.Create("log");
			f.AddParam("x", 0);
			f.AddParam("base", 10);
			f.code = (context, partialResult) => {
				FP x = context.GetLocalDouble("x");
				FP b = context.GetLocalDouble("base");
				FP result = FPMath.Log(x, b);
				return new Intrinsic.Result(result);
			};
			
			// lower
			//	Return a lower-case version of a string.
			//	May be called with function syntax or dot syntax.
			// self (string): string to lower-case
			// Returns: string with all capital letters converted to lowercase
			// Example: "Mo Spam".lower		returns "mo spam"
			// See also: upper
			f = Intrinsic.Create("lower");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value val = context.self;
				if (val is ValString) {
					string str = ((ValString)val).value;
					return new Intrinsic.Result(str.ToLower());
				}
				return new Intrinsic.Result(val);
			};

			// map type
			//	Returns a map that represents the map datatype in
			//	MiniScript's core type system.  This can be used with `isa`
			//	to check whether a variable refers to a map.  You can also
			//	assign new methods here to make them available to all maps.
			// Example: {1:"one"} isa map		returns 1
			// See also: number, string, list, funcRef
			f = Intrinsic.Create("map");
			f.code = (context, partialResult) => {
				if (context.vm.mapType == null) {
					context.vm.mapType = MapType().EvalCopy(context.vm.globalContext);
				}
				return new Intrinsic.Result(context.vm.mapType);
			};
			
			// number type
			//	Returns a map that represents the number datatype in
			//	MiniScript's core type system.  This can be used with `isa`
			//	to check whether a variable refers to a number.  You can also
			//	assign new methods here to make them available to all maps
			//	(though because of a limitation in MiniScript's parser, such
			//	methods do not work on numeric literals).
			// Example: 42 isa number		returns 1
			// See also: string, list, map, funcRef
			f = Intrinsic.Create("number");
			f.code = (context, partialResult) => {
				if (context.vm.numberType == null) {
					context.vm.numberType = NumberType().EvalCopy(context.vm.globalContext);
				}
				return new Intrinsic.Result(context.vm.numberType);
			};
			
			// pi
			//	Returns the universal constant π, that is, the ratio of
			//	a circle's circumference to its diameter.
			// Example: pi		returns 3.141593
			f = Intrinsic.Create("pi");
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FP.Pi);
			};

			// print
			//	Display the given value on the default output stream.  The
			//	exact effect may vary with the environment.  In most cases, the
			//	given string will be followed by the standard line delimiter
			//	(unless overridden with the second parameter).
			// s (any): value to print (converted to a string as needed)
			// delimiter (string or null): string to print after s; if null, use standard EOL
			// Returns: null
			// Example: print 6*7
			f = Intrinsic.Create("print");
			f.AddParam("s", ValString.empty);
			f.AddParam("delimiter");
			f.code = (context, partialResult) => {
				Value sVal = context.GetLocal("s");
				string s = (sVal == null ? "null" : sVal.ToString());
				Value delimiter = context.GetLocal("delimiter");
				if (delimiter == null) context.vm.standardOutput(s, true);
				else context.vm.standardOutput(s + delimiter.ToString(), false);
				return Intrinsic.Result.Null;
			};
				
			// pop
			//	Removes and	returns the last item in a list, or an arbitrary
			//	key of a map.  If the list or map is empty (or if called on
			//	any other data type), returns null.
			//	May be called with function syntax or dot syntax.
			// self (list or map): object to remove an element from the end of
			// Returns: value removed, or null
			// Example: [1, 2, 3].pop		returns (and removes) 3
			// See also: pull; push; remove
			f = Intrinsic.Create("pop");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.self;
				if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					if (list.Count < 1) return Intrinsic.Result.Null;
					Value result = list[list.Count-1];
					list.RemoveAt(list.Count-1);
					return new Intrinsic.Result(result);
				} else if (self is ValMap) {
					ValMap map = (ValMap)self;
					if (map.map.Count < 1) return Intrinsic.Result.Null;
					Value result = map.map.Keys.First();
					map.map.Remove(result);
					return new Intrinsic.Result(result);
				}
				return Intrinsic.Result.Null;
			};

			// pull
			//	Removes and	returns the first item in a list, or an arbitrary
			//	key of a map.  If the list or map is empty (or if called on
			//	any other data type), returns null.
			//	May be called with function syntax or dot syntax.
			// self (list or map): object to remove an element from the end of
			// Returns: value removed, or null
			// Example: [1, 2, 3].pull		returns (and removes) 1
			// See also: pop; push; remove
			f = Intrinsic.Create("pull");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.self;
				if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					if (list.Count < 1) return Intrinsic.Result.Null;
					Value result = list[0];
					list.RemoveAt(0);
					return new Intrinsic.Result(result);
				} else if (self is ValMap) {
					ValMap map = (ValMap)self;
					if (map.map.Count < 1) return Intrinsic.Result.Null;
					Value result = map.map.Keys.First();
					map.map.Remove(result);
					return new Intrinsic.Result(result);
				}
				return Intrinsic.Result.Null;
			};

			// push
			//	Appends an item to the end of a list, or inserts it into a map
			//	as a key with a value of 1.
			//	May be called with function syntax or dot syntax.
			// self (list or map): object to append an element to
			// Returns: self
			// See also: pop, pull, insert
			f = Intrinsic.Create("push");
			f.AddParam("self");
			f.AddParam("value");
			f.code = (context, partialResult) => {
				Value self = context.self;
				Value value = context.GetLocal("value");
				if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					list.Add(value);
					return new Intrinsic.Result(self);
				} else if (self is ValMap) {
					ValMap map = (ValMap)self;
					map.map[value] = ValNumber.one;
					return new Intrinsic.Result(self);
				}
				return Intrinsic.Result.Null;
			};

			// range
			//	Return a list containing a series of numbers within a range.
			// from (number, default 0): first number to include in the list
			// to (number, default 0): point at which to stop adding numbers to the list
			// step (number, optional): amount to add to the previous number on each step;
			//	defaults to 1 if to > from, or -1 if to < from
			// Example: range(50, 5, -10)		returns [50, 40, 30, 20, 10]
			f = Intrinsic.Create("range");
			f.AddParam("from", 0);
			f.AddParam("to", 0);
			f.AddParam("step");
			f.code = (context, partialResult) => {
				Value p0 = context.GetLocal("from");
				Value p1 = context.GetLocal("to");
				Value p2 = context.GetLocal("step");
				FP fromVal = p0.DoubleValue();
				FP toVal = p1.DoubleValue();
				FP step = (toVal >= fromVal ? 1 : -1);
				if (p2 is ValNumber) step = (p2 as ValNumber).value;
				if (step == 0) throw new RuntimeException("range() error (step==0)");
				List<Value> values = new List<Value>();
				int count = (int)((toVal - fromVal) / step) + 1;
				if (count > ValList.maxSize) throw new RuntimeException("list too large");
				try {
					values = new List<Value>(count);
					for (FP v = fromVal; step > 0 ? (v <= toVal) : (v >= toVal); v += step) {
						values.Add(TAC.Num(v));
					}
				} catch (SystemException e) {
					// uh-oh... probably out-of-memory exception; clean up and bail out
					values = null;
					throw(new LimitExceededException("range() error", e));
				}
				return new Intrinsic.Result(new ValList(values));
			};

			// refEquals
			//	Tests whether two values refer to the very same object (rather than
			//	merely representing the same value).  For numbers, this is the same
			//	as ==, but for strings, lists, and maps, it is reference equality.
			f = Intrinsic.Create("refEquals");
			f.AddParam("a");
			f.AddParam("b");
			f.code = (context, partialResult) => {
				Value a = context.GetLocal("a");
				Value b = context.GetLocal("b");
				bool result = false;
				if (a == null) {
					result = (b == null);
				} else if (a is ValNumber) {
					result = (b is ValNumber && a.DoubleValue() == b.DoubleValue());
				} else if (a is ValString) {
					result = (b is ValString && ReferenceEquals( ((ValString)a).value, ((ValString)b).value ));
				} else if (a is ValList) {
					result = (b is ValList && ReferenceEquals( ((ValList)a).values, ((ValList)b).values ));
				} else if (a is ValMap) {
					result = (b is ValMap && ReferenceEquals( ((ValMap)a).map, ((ValMap)b).map ));
				} else if (a is ValFunction) {
					result = (b is ValFunction && ReferenceEquals( ((ValFunction)a).function, ((ValFunction)b).function ));
				} else {
					result = (a.Equality(b) >= 1);
				}
				return new Intrinsic.Result(ValNumber.Truth(result));
			};

			// remove
			//	Removes part of a list, map, or string.  Exact behavior depends on
			//	the data type of self:
			// 		list: removes one element by its index; the list is mutated in place;
			//			returns null, and throws an error if the given index out of range
			//		map: removes one key/value pair by key; the map is mutated in place;
			//			returns 1 if key was found, 0 otherwise
			//		string:	returns a new string with the first occurrence of k removed
			//	May be called with function syntax or dot syntax.
			// self (list, map, or string): object to remove something from
			// k (any): index or substring to remove
			// Returns: (see above)
			// Example: a=["a","b","c"]; a.remove 1		leaves a == ["a", "c"]
			// Example: d={"ichi":"one"}; d.remove "ni"		returns 0
			// Example: "Spam".remove("S")		returns "pam"
			// See also: indexOf
			f = Intrinsic.Create("remove");
			f.AddParam("self");
			f.AddParam("k");
			f.code = (context, partialResult) => {
				Value self = context.self;
				Value k = context.GetLocal("k");
				if (self is ValMap) {
					ValMap selfMap = (ValMap)self;
					if (k == null) k = ValNull.instance;
					if (selfMap.map.ContainsKey(k)) {
						selfMap.map.Remove(k);
						return Intrinsic.Result.True;
					}
					return Intrinsic.Result.False;
				} else if (self is ValList) {
					if (k == null) throw new RuntimeException("argument to 'remove' must not be null");
					ValList selfList = (ValList)self;
					int idx = k.IntValue();
					if (idx < 0) idx += selfList.values.Count;
					Check.Range(idx, 0, selfList.values.Count-1);
					selfList.values.RemoveAt(idx);
					return Intrinsic.Result.Null;
				} else if (self is ValString) {
					if (k == null) throw new RuntimeException("argument to 'remove' must not be null");
					ValString selfStr = (ValString)self;
					string substr = k.ToString();
					int foundPos = selfStr.value.IndexOf(substr, StringComparison.Ordinal);
					if (foundPos < 0) return new Intrinsic.Result(self);
					return new Intrinsic.Result(selfStr.value.Remove(foundPos, substr.Length));
				}
				throw new TypeException("Type Error: 'remove' requires map, list, or string");
			};

			// replace
			//	Replace all matching elements of a list or map, or substrings of a string,
			//	with a new value.Lists and maps are mutated in place, and return themselves.
			//	Strings are immutable, so the original string is (of course) unchanged, but
			//	a new string with the replacement is returned.  Note that with maps, it is
			//	the values that are searched for and replaced, not the keys.
			// self (list, map, or string): object to replace elements of
			// oldval (any): value or substring to replace
			// newval (any): new value or substring to substitute where oldval is found
			// maxCount (number, optional): if given, replace no more than this many
			// Returns: modified list or map, or new string, with replacements done
			// Example: "Happy Pappy".replace("app", "ol")		returns "Holy Poly"
			// Example: [1,2,3,2,5].replace(2, 42)		returns (and mutates to) [1, 42, 3, 42, 5]
			// Example: d = {1: "one"}; d.replace("one", "ichi")		returns (and mutates to) {1: "ichi"}
			f = Intrinsic.Create("replace");
			f.AddParam("self");
			f.AddParam("oldval");
			f.AddParam("newval");
			f.AddParam("maxCount");
			f.code = (context, partialResult) => {
				Value self = context.self;
				if (self == null) throw new RuntimeException("argument to 'replace' must not be null");
				Value oldval = context.GetLocal("oldval");
				Value newval = context.GetLocal("newval");
				Value maxCountVal = context.GetLocal("maxCount");
				int maxCount = -1;
				if (maxCountVal != null) {
					maxCount = maxCountVal.IntValue();
					if (maxCount < 1) return new Intrinsic.Result(self);
				}
				int count = 0;
				if (self is ValMap) {
					ValMap selfMap = (ValMap)self;
					// C# doesn't allow changing even the values while iterating
					// over the keys.  So gather the keys to change, then change
					// them afterwards.
					List<Value> keysToChange = null;
					foreach (Value k in selfMap.map.Keys) {
						if (selfMap.map[k].Equality(oldval) == 1) {
							if (keysToChange == null) keysToChange = new List<Value>();
							keysToChange.Add(k);
							count++;
							if (maxCount > 0 && count == maxCount) break;
						}
					}
					if (keysToChange != null) foreach (Value k in keysToChange) {
						selfMap.map[k] = newval;
					}
					return new Intrinsic.Result(self);
				} else if (self is ValList) {
					ValList selfList = (ValList)self;
					int idx = -1;
					while (true) {
						idx = selfList.values.FindIndex(idx+1, x => x.Equality(oldval) == 1);
						if (idx < 0) break;
						selfList.values[idx] = newval;
						count++;
						if (maxCount > 0 && count == maxCount) break;
					}
					return new Intrinsic.Result(self);
				} else if (self is ValString) {
					string str = self.ToString();
					string oldstr = oldval == null ? "" : oldval.ToString();
					if (string.IsNullOrEmpty(oldstr)) throw new RuntimeException("replace: oldval argument is empty");
					string newstr = newval == null ? "" : newval.ToString();
					int idx = 0;
					while (true) {
						idx = str.IndexOf(oldstr, idx, StringComparison.Ordinal);
						if (idx < 0) break;
						str = str.Substring(0, idx) + newstr + str.Substring(idx + oldstr.Length);
						idx += newstr.Length;
						count++;
						if (maxCount > 0 && count == maxCount) break;
					}
					return new Intrinsic.Result(str);
				}
				throw new TypeException("Type Error: 'replace' requires map, list, or string");
			};

			// round
			//	Rounds a number to the specified number of decimal places.  If given
			//	a negative number for decimalPlaces, then rounds to a power of 10:
			//	-1 rounds to the nearest 10, -2 rounds to the nearest 100, etc.
			// x (number): number to round
			// decimalPlaces (number, defaults to 0): how many places past the decimal point to round to
			// Example: round(pi, 2)		returns 3.14
			// Example: round(12345, -3)		returns 12000
			f = Intrinsic.Create("round");
			f.AddParam("x", 0);
			//f.AddParam("decimalPlaces", 0);
			f.code = (context, partialResult) => {
                // TODO
				FP num = context.GetLocalDouble("x");
                num = FPMath.Round(num);
                /*
				int decimalPlaces = context.GetLocalInt("decimalPlaces");
				if (decimalPlaces >= 0) {
					if (decimalPlaces > 15) decimalPlaces = 15;
					num = Math.Round(num, decimalPlaces);
				} else {
					double pow10 = Math.Pow(10, -decimalPlaces);
					num = Math.Round(num / pow10) * pow10;
				}
                */
				return new Intrinsic.Result(num);
			};

            /*
			// rnd
			//	Generates a pseudorandom number between 0 and 1 (including 0 but
			//	not including 1).  If given a seed, then the generator is reset
			//	with that seed value, allowing you to create repeatable sequences
			//	of random numbers.  If you never specify a seed, then it is
			//	initialized automatically, generating a unique sequence on each run.
			// seed (number, optional): if given, reset the sequence with this value
			// Returns: pseudorandom number in the range [0,1)
			f = Intrinsic.Create("rnd");
			f.AddParam("seed");
			f.code = (context, partialResult) => {
				if (random == null) random = new Random();
				Value seed = context.GetLocal("seed");
				if (seed != null) random = new Random(seed.IntValue());
				return new Intrinsic.Result(random.NextDouble());
			};
            */

			// sign
			//	Return -1 for negative numbers, 1 for positive numbers, and 0 for zero.
			// x (number): number to get the sign of
			// Returns: sign of the number
			// Example: sign(-42.6)		returns -1
			f = Intrinsic.Create("sign");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Sign(context.GetLocalDouble("x")));
			};

			// sin
			//	Returns the sine of the given angle (in radians).
			// radians (number): angle, in radians, to get the sine of
			// Returns: sine of the given angle
			// Example: sin(pi/2)		returns 1
			f = Intrinsic.Create("sin");
			f.AddParam("radians", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Sin(context.GetLocalDouble("radians")));
			};
				
			// slice
			//	Return a subset of a string or list.  This is equivalent to using
			//	the square-brackets slice operator seq[from:to], but with ordinary
			//	function syntax.
			// seq (string or list): sequence to get a subsequence of
			// from (number, default 0): 0-based index to the first element to return (if negative, counts from the end)
			// to (number, optional): 0-based index of first element to *not* include in the result
			//		(if negative, count from the end; if omitted, return the rest of the sequence)
			// Returns: substring or sublist
			// Example: slice("Hello", -2)		returns "lo"
			// Example: slice(["a","b","c","d"], 1, 3)		returns ["b", "c"]
			f = Intrinsic.Create("slice");
			f.AddParam("seq");
			f.AddParam("from", 0);
			f.AddParam("to");
			f.code = (context, partialResult) => {
				Value seq = context.GetLocal("seq");
				int fromIdx = context.GetLocalInt("from");
				Value toVal = context.GetLocal("to");
				int toIdx = 0;
				if (toVal != null) toIdx = toVal.IntValue();
				if (seq is ValList) {
					List<Value> list = ((ValList)seq).values;
					if (fromIdx < 0) fromIdx += list.Count;
					if (fromIdx < 0) fromIdx = 0;
					if (toVal == null) toIdx = list.Count;
					if (toIdx < 0) toIdx += list.Count;
					if (toIdx > list.Count) toIdx = list.Count;
					ValList slice = new ValList();
					if (fromIdx < list.Count && toIdx > fromIdx) {
						for (int i = fromIdx; i < toIdx; i++) {
							slice.values.Add(list[i]);
						}
					}
					return new Intrinsic.Result(slice);
				} else if (seq is ValString) {
					string str = ((ValString)seq).value;
					if (fromIdx < 0) fromIdx += str.Length;
					if (fromIdx < 0) fromIdx = 0;
					if (toVal == null) toIdx = str.Length;
					if (toIdx < 0) toIdx += str.Length;
					if (toIdx > str.Length) toIdx = str.Length;
					if (toIdx - fromIdx <= 0) return Intrinsic.Result.EmptyString;
					return new Intrinsic.Result(str.Substring(fromIdx, toIdx - fromIdx));
				}
				return Intrinsic.Result.Null;
			};
			
			// sort
			//	Sorts a list in place.  With null or no argument, this sorts the
			//	list elements by their own values.  With the byKey argument, each
			//	element is indexed by that argument, and the elements are sorted
			//	by the result.  (This only works if the list elements are maps, or
			//	they are lists and byKey is an integer index.)
			// self (list): list to sort
			// byKey (optional): if given, sort each element by indexing with this key
			// ascending (optional, default true): if false, sort in descending order
			// Returns: self (which has been sorted in place)
			// Example: a = [5,3,4,1,2]; a.sort		results in a == [1, 2, 3, 4, 5]
			// See also: shuffle
			f = Intrinsic.Create("sort");
			f.AddParam("self");
			f.AddParam("byKey");
			f.AddParam("ascending", ValNumber.one);
			f.code = (context, partialResult) => {
				Value self = context.self;
				ValList list = self as ValList;
				if (list == null || list.values.Count < 2) return new Intrinsic.Result(self);

				IComparer<Value> sorter;
				if (context.GetVar("ascending").BoolValue()) sorter = ValueSorter.instance;
				else sorter = ValueReverseSorter.instance;

				Value byKey = context.GetLocal("byKey");
				if (byKey == null) {
					// Simple case: sort the values as themselves
					list.values = list.values.OrderBy((arg) => arg, sorter).ToList();
				} else {
					// Harder case: sort by a key.
					int count = list.values.Count;
					KeyedValue[] arr = new KeyedValue[count];
					for (int i=0; i<count; i++) {
						arr[i].value = list.values[i];
						//arr[i].valueIndex = i;
					}
					// The key for each item will be the item itself, unless it is a map, in which
					// case it's the item indexed by the given key.  (Works too for lists if our
					// index is an integer.)
					int byKeyInt = byKey.IntValue();
					for (int i=0; i<count; i++) {
						Value item = list.values[i];
						if (item is ValMap) arr[i].sortKey = ((ValMap)item).Lookup(byKey);
						else if (item is ValList) {
							ValList itemList = (ValList)item;
							if (byKeyInt > -itemList.values.Count && byKeyInt < itemList.values.Count) arr[i].sortKey = itemList.values[byKeyInt];
							else arr[i].sortKey = null;
						}
					}
					// Now sort our list of keyed values, by key
					var sortedArr = arr.OrderBy((arg) => arg.sortKey, sorter);
					// And finally, convert that back into our list
					int idx=0;
					foreach (KeyedValue kv in sortedArr) {
						list.values[idx++] = kv.value;
					}
				}
				return new Intrinsic.Result(list);
			};

			// split
			//	Split a string into a list, by some delimiter.
			//	May be called with function syntax or dot syntax.
			// self (string): string to split
			// delimiter (string, default " "): substring to split on
			// maxCount (number, default -1): if > 0, split into no more than this many strings
			// Returns: list of substrings found by splitting on delimiter
			// Example: "foo bar baz".split		returns ["foo", "bar", "baz"]
			// Example: "foo bar baz".split("a", 2)		returns ["foo b", "r baz"]
			// See also: join
			f = Intrinsic.Create("split");
			f.AddParam("self");
			f.AddParam("delimiter", " ");
			f.AddParam("maxCount", -1);
			f.code = (context, partialResult) => {
				string self = context.self.ToString();
				string delim = context.GetLocalString("delimiter");
				int maxCount = context.GetLocalInt("maxCount");
				ValList result = new ValList();
				int pos = 0;
				while (pos < self.Length) {
					int nextPos;
					if (maxCount >= 0 && result.values.Count == maxCount - 1) nextPos = self.Length;
					else if (delim.Length == 0) nextPos = pos+1;
					else nextPos = self.IndexOf(delim, pos, StringComparison.Ordinal);
					if (nextPos < 0) nextPos = self.Length;
					result.values.Add(new ValString(self.Substring(pos, nextPos - pos)));
					pos = nextPos + delim.Length;
					if (pos == self.Length && delim.Length > 0) result.values.Add(ValString.empty);
				}
				return new Intrinsic.Result(result);
			};

			// sqrt
			//	Returns the square root of a number.
			// x (number): number to get the square root of
			// Returns: square root of x
			// Example: sqrt(1764)		returns 42
			f = Intrinsic.Create("sqrt");
			f.AddParam("x", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Sqrt(context.GetLocalDouble("x")));
			};

			// stackTrace: get a list describing the call stack.
			f = Intrinsic.Create("stackTrace");
			f.code = (context, partialResult) => {
				TAC.Machine vm = context.vm;
				var _stackAtBreak = new ValString("_stackAtBreak");
				if (vm.globalContext.variables.ContainsKey(_stackAtBreak)) {
					// We have a stored stack from a break or exit.
					// So, display that.  The host app should clear this when starting a 'run'
					// so it never interferes with showing a more up-to-date stack during a run.
					return new Intrinsic.Result(vm.globalContext.variables.map[_stackAtBreak]);
				}
				// Otherwise, build a stack now from the state of the VM.
				ValList result = StackList(vm);
				return new Intrinsic.Result(result);
			};

			// str
			//	Convert any value to a string.
			// x (any): value to convert
			// Returns: string representation of the given value
			// Example: str(42)		returns "42"
			// See also: val
			f = Intrinsic.Create("str");
			f.AddParam("x", ValString.empty);
			f.code = (context, partialResult) => {		
				var x = context.GetLocal("x");
				if (x == null) return new Intrinsic.Result(ValString.empty);
				return new Intrinsic.Result(x.ToString());
			};

			// string type
			//	Returns a map that represents the string datatype in
			//	MiniScript's core type system.  This can be used with `isa`
			//	to check whether a variable refers to a string.  You can also
			//	assign new methods here to make them available to all strings.
			// Example: "Hello" isa string		returns 1
			// See also: number, list, map, funcRef
			f = Intrinsic.Create("string");
			f.code = (context, partialResult) => {
				if (context.vm.stringType == null) {
					context.vm.stringType = StringType().EvalCopy(context.vm.globalContext);
				}
				return new Intrinsic.Result(context.vm.stringType);
			};

			// shuffle
			//	Randomize the order of elements in a list, or the mappings from
			//	keys to values in a map.  This is done in place.
			// self (list or map): object to shuffle
			// Returns: null
			f = Intrinsic.Create("shuffle");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.self;
                Frame f = context.interpreter.hostData as Frame;
                if (self is ValList) {
					List<Value> list = ((ValList)self).values;
					// We'll do a Fisher-Yates shuffle, i.e., swap each element
					// with a randomly selected one.
					for (int i=list.Count-1; i >= 1; i--) {
						int j = f.RNG->Next(0, i + 1);
						Value temp = list[j];
						list[j] = list[i];
						list[i] = temp;
					}
				} else if (self is ValMap) {
					Dictionary<Value, Value> map = ((ValMap)self).map;
					// Fisher-Yates again, but this time, what we're swapping
					// is the values associated with the keys, not the keys themselves.
					List<Value> keys = System.Linq.Enumerable.ToList(map.Keys);
					for (int i=keys.Count-1; i >= 1; i--) {
						int j = f.RNG->Next(0, i + 1);
                        Value keyi = keys[i];
						Value keyj = keys[j];
						Value temp = map[keyj];
						map[keyj] = map[keyi];
						map[keyi] = temp;
					}
				}
				return Intrinsic.Result.Null;
			};

			// sum
			//	Returns the total of all elements in a list, or all values in a map.
			// self (list or map): object to sum
			// Returns: result of adding up all values in self
			// Example: range(3).sum		returns 6 (3 + 2 + 1 + 0)
			f = Intrinsic.Create("sum");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value val = context.self;
				FP sum = 0;
				if (val is ValList) {
					List<Value> list = ((ValList)val).values;
					foreach (Value v in list) {
						sum += v.DoubleValue();
					}
				} else if (val is ValMap) {
					Dictionary<Value, Value> map = ((ValMap)val).map;
					foreach (Value v in map.Values) {
						sum += v.DoubleValue();
					}
				}
				return new Intrinsic.Result(sum);
			};

			// tan
			//	Returns the tangent of the given angle (in radians).
			// radians (number): angle, in radians, to get the tangent of
			// Returns: tangent of the given angle
			// Example: tan(pi/4)		returns 1
			f = Intrinsic.Create("tan");
			f.AddParam("radians", 0);
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(FPMath.Tan(context.GetLocalDouble("radians")));
			};

            /*
			// time
			//	Returns the number of seconds since the script started running.
			f = Intrinsic.Create("time");
			f.code = (context, partialResult) => {
				return new Intrinsic.Result(context.vm.runTime);
			};
            */
			
			// upper
			//	Return an upper-case (all capitals) version of a string.
			//	May be called with function syntax or dot syntax.
			// self (string): string to upper-case
			// Returns: string with all lowercase letters converted to capitals
			// Example: "Mo Spam".upper		returns "MO SPAM"
			// See also: lower
			f = Intrinsic.Create("upper");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value val = context.self;
				if (val is ValString) {
					string str = ((ValString)val).value;
					return new Intrinsic.Result(str.ToUpper());
				}
				return new Intrinsic.Result(val);
			};
			
			// val
			//	Return the numeric value of a given string.  (If given a number,
			//	returns it as-is; if given a list or map, returns null.)
			//	May be called with function syntax or dot syntax.
			// self (string or number): string to get the value of
			// Returns: numeric value of the given string
			// Example: "1234.56".val		returns 1234.56
			// See also: str
			f = Intrinsic.Create("val");
			f.AddParam("self", 0);
			f.code = (context, partialResult) => {
				Value val = context.self;
				if (val is ValNumber) return new Intrinsic.Result(val);
				if (val is ValString) {
					FP value = 0;
                    try {
					    value = FP.FromString(val.ToString());
                    } catch { }
					return new Intrinsic.Result(value);
				}
				return Intrinsic.Result.Null;
			};

			// values
			//	Returns the values of a dictionary, or the characters of a string.
			//  (Returns any other value as-is.)
			//	May be called with function syntax or dot syntax.
			// self (any): object to get the values of.
			// Example: d={1:"one", 2:"two"}; d.values		returns ["one", "two"]
			// Example: "abc".values		returns ["a", "b", "c"]
			// See also: indexes
			f = Intrinsic.Create("values");
			f.AddParam("self");
			f.code = (context, partialResult) => {
				Value self = context.self;
				if (self is ValMap) {
					ValMap map = (ValMap)self;
					List<Value> values = new List<Value>(map.map.Values);
					return new Intrinsic.Result(new ValList(values));
				} else if (self is ValString) {
					string str = ((ValString)self).value;
					List<Value> values = new List<Value>(str.Length);
					for (int i = 0; i < str.Length; i++) {
						values.Add(TAC.Str(str[i].ToString()));
					}
					return new Intrinsic.Result(new ValList(values));
				}
				return new Intrinsic.Result(self);
			};

            /*
			// version
			//	Get a map with information about the version of MiniScript and
			//	the host environment that you're currently running.  This will
			//	include at least the following keys:
			//		miniscript: a string such as "1.5"
			//		buildDate: a date in yyyy-mm-dd format, like "2020-05-28"
			//		host: a number for the host major and minor version, like 0.9
			//		hostName: name of the host application, e.g. "Mini Micro"
			//		hostInfo: URL or other short info about the host app
			f = Intrinsic.Create("version");
			f.code = (context, partialResult) => {
				if (context.vm.versionMap == null) {
					//UnityEngine.Debug.Log("in version intrinsic, and versionMap == null");
					var d = new ValMap();
					d["miniscript"] = new ValString("1.6.2");
			
					// Getting the build date is annoyingly hard in C#.
					// This will work if the assembly.cs file uses the version format: 1.0.*
					DateTime buildDate;
					System.Version version = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version;
					buildDate = new DateTime(2000, 1, 1);
					buildDate = buildDate.AddDays(version.Build);
					buildDate = buildDate.AddSeconds(version.Revision * 2);
					d["buildDate"] = new ValString(buildDate.ToString("yyyy-MM-dd"));

					d["host"] = new ValNumber(HostInfo.version);
					d["hostName"] = new ValString(HostInfo.name);
					d["hostInfo"] = new ValString(HostInfo.info);

					context.vm.versionMap = d;
				}
				return new Intrinsic.Result(context.vm.versionMap);
			};

			// wait
			//	Pause execution of this script for some amount of time.
			// seconds (default 1.0): how many seconds to wait
			// Example: wait 2.5		pauses the script for 2.5 seconds
			// See also: time, yield
			f = Intrinsic.Create("wait");
			f.AddParam("seconds", 1);
			f.code = (context, partialResult) => {
				double now = context.vm.runTime;
				if (partialResult == null) {
					// Just starting our wait; calculate end time and return as partial result
					double interval = context.GetLocalDouble("seconds");
					return new Intrinsic.Result(new ValNumber(now + interval), false);
				} else {
					// Continue until current time exceeds the time in the partial result
					if (now > partialResult.result.DoubleValue()) return Intrinsic.Result.Null;
					return partialResult;
				}
			};
            
            */
            // yield
            //	Pause the execution of the script until the next "tick" of
            //	the host app.  In Mini Micro, for example, this waits until
            //	the next 60Hz frame.  Exact meaning may vary, but generally
            //	if you're doing something in a tight loop, calling yield is
            //	polite to the host app or other scripts.
            f = Intrinsic.Create("yield");
			f.code = (context, partialResult) => {
				context.vm.yielding = true;
				return Intrinsic.Result.Null;
			};
        }

		// Helper method to compile a call to Slice (when invoked directly via slice syntax).
		public static void CompileSlice(List<TAC.Line> code, Value list, Value fromIdx, Value toIdx, int resultTempNum) {
			code.Add(new TAC.Line(null, TAC.Line.Op.PushParam, list));
			code.Add(new TAC.Line(null, TAC.Line.Op.PushParam, fromIdx == null ? TAC.Num(0) : fromIdx));
			code.Add(new TAC.Line(null, TAC.Line.Op.PushParam, toIdx));// toIdx == null ? TAC.Num(0) : toIdx));
			ValFunction func = Intrinsic.GetByName("slice").GetFunc();
			code.Add(new TAC.Line(TAC.LTemp(resultTempNum), TAC.Line.Op.CallFunctionA, func, TAC.Num(3)));
		}
		
		/// <summary>
		/// FunctionType: a static map that represents the Function type.
		/// </summary>
		public static ValMap FunctionType() {
			if (_functionType == null) {
				_functionType = new ValMap();
			}
			return _functionType;
		}
		static ValMap _functionType = null;
		
		/// <summary>
		/// ListType: a static map that represents the List type, and provides
		/// intrinsic methods that can be invoked on it via dot syntax.
		/// </summary>
		public static ValMap ListType() {
			if (_listType == null) {
				_listType = new ValMap();
				_listType["hasIndex"] = Intrinsic.GetByName("hasIndex").GetFunc();
				_listType["indexes"] = Intrinsic.GetByName("indexes").GetFunc();
				_listType["indexOf"] = Intrinsic.GetByName("indexOf").GetFunc();
				_listType["insert"] = Intrinsic.GetByName("insert").GetFunc();
				_listType["join"] = Intrinsic.GetByName("join").GetFunc();
				_listType["len"] = Intrinsic.GetByName("len").GetFunc();
				_listType["pop"] = Intrinsic.GetByName("pop").GetFunc();
				_listType["pull"] = Intrinsic.GetByName("pull").GetFunc();
				_listType["push"] = Intrinsic.GetByName("push").GetFunc();
				_listType["shuffle"] = Intrinsic.GetByName("shuffle").GetFunc();
				_listType["sort"] = Intrinsic.GetByName("sort").GetFunc();
				_listType["sum"] = Intrinsic.GetByName("sum").GetFunc();
				_listType["remove"] = Intrinsic.GetByName("remove").GetFunc();
				_listType["replace"] = Intrinsic.GetByName("replace").GetFunc();
				_listType["values"] = Intrinsic.GetByName("values").GetFunc();
			}
			return _listType;
		}
		static ValMap _listType = null;
		
		/// <summary>
		/// StringType: a static map that represents the String type, and provides
		/// intrinsic methods that can be invoked on it via dot syntax.
		/// </summary>
		public static ValMap StringType() {
			if (_stringType == null) {
				_stringType = new ValMap();
				_stringType["hasIndex"] = Intrinsic.GetByName("hasIndex").GetFunc();
				_stringType["indexes"] = Intrinsic.GetByName("indexes").GetFunc();
				_stringType["indexOf"] = Intrinsic.GetByName("indexOf").GetFunc();
				_stringType["insert"] = Intrinsic.GetByName("insert").GetFunc();
				_stringType["code"] = Intrinsic.GetByName("code").GetFunc();
				_stringType["len"] = Intrinsic.GetByName("len").GetFunc();
				_stringType["lower"] = Intrinsic.GetByName("lower").GetFunc();
				_stringType["val"] = Intrinsic.GetByName("val").GetFunc();
				_stringType["remove"] = Intrinsic.GetByName("remove").GetFunc();
				_stringType["replace"] = Intrinsic.GetByName("replace").GetFunc();
				_stringType["split"] = Intrinsic.GetByName("split").GetFunc();
				_stringType["upper"] = Intrinsic.GetByName("upper").GetFunc();
				_stringType["values"] = Intrinsic.GetByName("values").GetFunc();
			}
			return _stringType;
		}
		static ValMap _stringType = null;
		
		/// <summary>
		/// MapType: a static map that represents the Map type, and provides
		/// intrinsic methods that can be invoked on it via dot syntax.
		/// </summary>
		public static ValMap MapType() {
			if (_mapType == null) {
				_mapType = new ValMap();
				_mapType["hasIndex"] = Intrinsic.GetByName("hasIndex").GetFunc();
				_mapType["indexes"] = Intrinsic.GetByName("indexes").GetFunc();
				_mapType["indexOf"] = Intrinsic.GetByName("indexOf").GetFunc();
				_mapType["len"] = Intrinsic.GetByName("len").GetFunc();
				_mapType["pop"] = Intrinsic.GetByName("pop").GetFunc();
				_mapType["push"] = Intrinsic.GetByName("push").GetFunc();
				_mapType["pull"] = Intrinsic.GetByName("pull").GetFunc();
				_mapType["shuffle"] = Intrinsic.GetByName("shuffle").GetFunc();
				_mapType["sum"] = Intrinsic.GetByName("sum").GetFunc();
				_mapType["remove"] = Intrinsic.GetByName("remove").GetFunc();
				_mapType["replace"] = Intrinsic.GetByName("replace").GetFunc();
				_mapType["values"] = Intrinsic.GetByName("values").GetFunc();
			}
			return _mapType;
		}
		static ValMap _mapType = null;
		
		/// <summary>
		/// NumberType: a static map that represents the Number type.
		/// </summary>
		public static ValMap NumberType() {
			if (_numberType == null) {
				_numberType = new ValMap();
			}
			return _numberType;
		}
		static ValMap _numberType = null;
		
		
	}
}
