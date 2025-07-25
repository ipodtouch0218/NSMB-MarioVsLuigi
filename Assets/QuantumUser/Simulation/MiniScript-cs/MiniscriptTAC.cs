/*	MiniscriptTAC.cs

This file defines the three-address code (TAC) which represents compiled
MiniScript code.  TAC is sort of a pseudo-assembly language, composed of
simple instructions containing an opcode and up to three variable/value 
references.

This is all internal MiniScript virtual machine code.  You don't need to
deal with it directly (see MiniscriptInterpreter instead).

*/

using Photon.Deterministic;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Miniscript {

	public static class TAC {

		public class Line {
			public enum Op {
				Noop = 0,
				AssignA,
				AssignImplicit,
				APlusB,
				AMinusB,
				ATimesB,
				ADividedByB,
				AModB,
				APowB,
				AEqualB,
				ANotEqualB,
				AGreaterThanB,
				AGreatOrEqualB,
				ALessThanB,
				ALessOrEqualB,
				AisaB,
				AAndB,
				AOrB,
				BindAssignA,
				CopyA,
				NewA,
				NotA,
				GotoA,
				GotoAifB,
				GotoAifTrulyB,
				GotoAifNotB,
				PushParam,
				CallFunctionA,
				CallIntrinsicA,
				ReturnA,
				ElemBofA,
				ElemBofIterA,
				LengthOfA
			}

			public Value lhs;
			public Op op;
			public Value rhsA;
			public Value rhsB;
//			public string comment;
			public SourceLoc location;

			public Line(Value lhs, Op op, Value rhsA=null, Value rhsB=null) {
				this.lhs = lhs;
				this.op = op;
				this.rhsA = rhsA;
				this.rhsB = rhsB;
			}
			
			public override int GetHashCode() {
				return lhs.GetHashCode() ^ op.GetHashCode() ^ rhsA.GetHashCode() ^ rhsB.GetHashCode() ^ location.GetHashCode();
			}
			
			public override bool Equals(object obj) {
				if (!(obj is Line)) return false;
				Line b = (Line)obj;
				return op == b.op && lhs == b.lhs && rhsA == b.rhsA && rhsB == b.rhsB && location == b.location;
			}
			
			public override string ToString() {
				string text;
				switch (op) {
				case Op.AssignA:
					text = string.Format("{0} := {1}", lhs, rhsA);
					break;
				case Op.AssignImplicit:
					text = string.Format("_ := {0}", rhsA);
					break;
				case Op.APlusB:
					text = string.Format("{0} := {1} + {2}", lhs, rhsA, rhsB);
					break;
				case Op.AMinusB:
					text = string.Format("{0} := {1} - {2}", lhs, rhsA, rhsB);
					break;
				case Op.ATimesB:
					text = string.Format("{0} := {1} * {2}", lhs, rhsA, rhsB);
					break;
				case Op.ADividedByB:
					text = string.Format("{0} := {1} / {2}", lhs, rhsA, rhsB);
					break;
				case Op.AModB:
					text = string.Format("{0} := {1} % {2}", lhs, rhsA, rhsB);
					break;
				case Op.APowB:
					text = string.Format("{0} := {1} ^ {2}", lhs, rhsA, rhsB);
					break;
				case Op.AEqualB:
					text = string.Format("{0} := {1} == {2}", lhs, rhsA, rhsB);
					break;
				case Op.ANotEqualB:
					text = string.Format("{0} := {1} != {2}", lhs, rhsA, rhsB);
					break;
				case Op.AGreaterThanB:
					text = string.Format("{0} := {1} > {2}", lhs, rhsA, rhsB);
					break;
				case Op.AGreatOrEqualB:
					text = string.Format("{0} := {1} >= {2}", lhs, rhsA, rhsB);
					break;
				case Op.ALessThanB:
					text = string.Format("{0} := {1} < {2}", lhs, rhsA, rhsB);
					break;
				case Op.ALessOrEqualB:
					text = string.Format("{0} := {1} <= {2}", lhs, rhsA, rhsB);
					break;
				case Op.AAndB:
					text = string.Format("{0} := {1} and {2}", lhs, rhsA, rhsB);
					break;
				case Op.AOrB:
					text = string.Format("{0} := {1} or {2}", lhs, rhsA, rhsB);
					break;
				case Op.AisaB:
					text = string.Format("{0} := {1} isa {2}", lhs, rhsA, rhsB);
					break;
				case Op.BindAssignA:
					text = string.Format("{0} := {1}; {0}.outerVars=", rhsA, rhsB);
					break;
				case Op.CopyA:
					text = string.Format("{0} := copy of {1}", lhs, rhsA);
					break;
				case Op.NewA:
					text = string.Format("{0} := new {1}", lhs, rhsA);
					break;
				case Op.NotA:
					text = string.Format("{0} := not {1}", lhs, rhsA);
					break;
				case Op.GotoA:
					text = string.Format("goto {0}", rhsA);
					break;
				case Op.GotoAifB:
					text = string.Format("goto {0} if {1}", rhsA, rhsB);
					break;
				case Op.GotoAifTrulyB:
					text = string.Format("goto {0} if truly {1}", rhsA, rhsB);
					break;
				case Op.GotoAifNotB:
					text = string.Format("goto {0} if not {1}", rhsA, rhsB);
					break;
				case Op.PushParam:
					text = string.Format("push param {0}", rhsA);
					break;
				case Op.CallFunctionA:
					text = string.Format("{0} := call {1} with {2} args", lhs, rhsA, rhsB);
					break;
				case Op.CallIntrinsicA:
					text = string.Format("intrinsic {0}", Intrinsic.GetByID(rhsA.IntValue()));
					break;
				case Op.ReturnA:
					text = string.Format("{0} := {1}; return", lhs, rhsA);
					break;
				case Op.ElemBofA:
					text = string.Format("{0} = {1}[{2}]", lhs, rhsA, rhsB);
					break;
				case Op.ElemBofIterA:
					text = string.Format("{0} = {1} iter {2}", lhs, rhsA, rhsB);
					break;
				case Op.LengthOfA:
					text = string.Format("{0} = len({1})", lhs, rhsA);
					break;
				default:
					throw new RuntimeException("unknown opcode: " + op);
					
				}
				//if (comment != null) text = text + "\t// " + comment;
				if (location != null) text = text + "\t// " + location;
				return text;
			}

			/// <summary>
			/// Evaluate this line and return the value that would be stored
			/// into the lhs.
			/// </summary>
			public Value Evaluate(Context context) {
				if (op == Op.AssignA || op == Op.ReturnA || op == Op.AssignImplicit) {
					// Assignment is a bit of a special case.  It's EXTREMELY common
					// in TAC, so needs to be efficient, but we have to watch out for
					// the case of a RHS that is a list or map.  This means it was a
					// literal in the source, and may contain references that need to
					// be evaluated now.
					if (rhsA is ValList || rhsA is ValMap) {
						return rhsA.FullEval(context);
					} else if (rhsA == null) {
						return null;
					} else {
						return rhsA.Val(context);
					}
				}
				if (op == Op.CopyA) {
					// This opcode is used for assigning a literal.  We actually have
					// to copy the literal, in the case of a mutable object like a
					// list or map, to ensure that if the same code executes again,
					// we get a new, unique object.
					if (rhsA is ValList) {
						return ((ValList)rhsA).EvalCopy(context);
					} else if (rhsA is ValMap) {
						return ((ValMap)rhsA).EvalCopy(context);
					} else if (rhsA == null) {
						return null;
					} else {
						return rhsA.Val(context);
					}
				}

				Value opA = rhsA!=null ? rhsA.Val(context) : null;
				Value opB = rhsB!=null ? rhsB.Val(context) : null;

				if (op == Op.AisaB) {
					if (opA == null) return ValNumber.Truth(opB == null);
					return ValNumber.Truth(opA.IsA(opB, context.vm));
				}

				if (op == Op.NewA) {
					// Create a new map, and set __isa on it to operand A (after 
					// verifying that this is a valid map to subclass).
					if (!(opA is ValMap)) {
						throw new RuntimeException("argument to 'new' must be a map");
					} else if (opA == context.vm.stringType) {
						throw new RuntimeException("invalid use of 'new'; to create a string, use quotes, e.g. \"foo\"");
					} else if (opA == context.vm.listType) {
						throw new RuntimeException("invalid use of 'new'; to create a list, use square brackets, e.g. [1,2]");
					} else if (opA == context.vm.numberType) {
						throw new RuntimeException("invalid use of 'new'; to create a number, use a numeric literal, e.g. 42");
					} else if (opA == context.vm.functionType) {
						throw new RuntimeException("invalid use of 'new'; to create a function, use the 'function' keyword");
					}
					ValMap newMap = new ValMap();
					newMap.SetElem(ValString.magicIsA, opA);
					return newMap;
				}

				if (op == Op.ElemBofA && opB is ValString) {
					// You can now look for a string in almost anything...
					// and we have a convenient (and relatively fast) method for it:
					ValMap ignored;
					return ValSeqElem.Resolve(opA, ((ValString)opB).value, context, out ignored);
				}

				// check for special cases of comparison to null (works with any type)
				if (op == Op.AEqualB && (opA == null || opB == null)) {
					return ValNumber.Truth(opA == opB);
				}
				if (op == Op.ANotEqualB && (opA == null || opB == null)) {
					return ValNumber.Truth(opA != opB);
				}
				
				// check for implicit coersion of other types to string; this happens
				// when either side is a string and the operator is addition.
				if ((opA is ValString || opB is ValString) && op == Op.APlusB) {
					if (opA == null) return opB;
					if (opB == null) return opA;
					string sA = opA.ToString(context.vm);
					string sB = opB.ToString(context.vm);
					if (sA.Length + sB.Length > ValString.maxSize) throw new LimitExceededException("string too large");
					return new ValString(sA + sB);
				}

				if (opA is ValNumber) {
					FP fA = ((ValNumber)opA).value;
					switch (op) {
					case Op.GotoA:
						context.lineNum = (int)fA;
						return null;
					case Op.GotoAifB:
						if (opB != null && opB.BoolValue()) context.lineNum = (int)fA;
						return null;
					case Op.GotoAifTrulyB:
						{
							// Unlike GotoAifB, which branches if B has any nonzero
							// value (including 0.5 or 0.001), this branches only if
							// B is TRULY true, i.e., its integer value is nonzero.
							// (Used for short-circuit evaluation of "or".)
							int i = 0;
							if (opB != null) i = opB.IntValue();
							if (i != 0) context.lineNum = (int)fA;
							return null;
						}
					case Op.GotoAifNotB:
						if (opB == null || !opB.BoolValue()) context.lineNum = (int)fA;
						return null;
					case Op.CallIntrinsicA:
						// NOTE: intrinsics do not go through NextFunctionContext.  Instead
						// they execute directly in the current context.  (But usually, the
						// current context is a wrapper function that was invoked via
						// Op.CallFunction, so it got a parameter context at that time.)
						Intrinsic.Result result = Intrinsic.Execute((int)fA, context, context.partialResult);
						if (result.done) {
							context.partialResult = null;
							return result.result;
						}
						// OK, this intrinsic function is not yet done with its work.
						// We need to stay on this same line and call it again with 
						// the partial result, until it reports that its job is complete.
						context.partialResult = result;
						context.lineNum--;
						return null;
					case Op.NotA:
						return new ValNumber(1 - AbsClamp01(fA));
					}
					if (opB is ValNumber || opB == null) {
						FP fB = opB != null ? ((ValNumber)opB).value : 0;
						switch (op) {
						case Op.APlusB:
							return new ValNumber(fA + fB);
						case Op.AMinusB:
							return new ValNumber(fA - fB);
						case Op.ATimesB:
							return new ValNumber(fA * fB);
						case Op.ADividedByB:
							return new ValNumber(fA / fB);
						case Op.AModB:
							return new ValNumber(fA % fB);
						//case Op.APowB:
						//	return new ValNumber(Math.Pow(fA, fB));
						case Op.AEqualB:
							return ValNumber.Truth(fA == fB);
						case Op.ANotEqualB:
							return ValNumber.Truth(fA != fB);
						case Op.AGreaterThanB:
							return ValNumber.Truth(fA > fB);
						case Op.AGreatOrEqualB:
							return ValNumber.Truth(fA >= fB);
						case Op.ALessThanB:
							return ValNumber.Truth(fA < fB);
						case Op.ALessOrEqualB:
							return ValNumber.Truth(fA <= fB);
						case Op.AAndB:
							if (!(opB is ValNumber)) fB = opB != null && opB.BoolValue() ? 1 : 0;
							return new ValNumber(AbsClamp01(fA * fB));
						case Op.AOrB:
							if (!(opB is ValNumber)) fB = opB != null && opB.BoolValue() ? 1 : 0;
							return new ValNumber(AbsClamp01(fA + fB - fA * fB));
						default:
							break;
						}
					}
					// Handle equality testing between a number (opA) and a non-number (opB).
					// These are always considered unequal.
					if (op == Op.AEqualB) return ValNumber.zero;
					if (op == Op.ANotEqualB) return ValNumber.one;

				} else if (opA is ValString) {
					string sA = ((ValString)opA).value;
					if (op == Op.ATimesB || op == Op.ADividedByB) {
						FP factor = 0;
                        if (op == Op.ATimesB) {
                            Check.Type(opB, typeof(ValNumber), "string replication");
                            factor = ((ValNumber) opB).value;
                        } else {
                            Check.Type(opB, typeof(ValNumber), "string division");
                            try {
                                factor = (FP) 1 / ((ValNumber) opB).value;
                            } catch (DivideByZeroException) {
                                return null;
                            }
                        }
						if (factor <= 0) return ValString.empty;
						int repeats = (int)factor;
						if (repeats * sA.Length > ValString.maxSize) throw new LimitExceededException("string too large");
						var result = new System.Text.StringBuilder();
						for (int i = 0; i < repeats; i++) result.Append(sA);
						int extraChars = (int)(sA.Length * (factor - repeats));
						if (extraChars > 0) result.Append(sA.Substring(0, extraChars));
						return new ValString(result.ToString());						
					}
					if (op == Op.ElemBofA || op == Op.ElemBofIterA) {
						return ((ValString)opA).GetElem(opB);
					}
					if (opB == null || opB is ValString) {
						string sB = (opB == null ? null : opB.ToString(context.vm));
						switch (op) {
							case Op.AMinusB: {
									if (opB == null) return opA;
									if (sA.EndsWith(sB)) sA = sA.Substring(0, sA.Length - sB.Length);
									return new ValString(sA);
								}
							case Op.NotA:
								return ValNumber.Truth(string.IsNullOrEmpty(sA));
							case Op.AEqualB:
								return ValNumber.Truth(string.Equals(sA, sB));
							case Op.ANotEqualB:
								return ValNumber.Truth(!string.Equals(sA, sB));
							case Op.AGreaterThanB:
								return ValNumber.Truth(string.Compare(sA, sB, StringComparison.Ordinal) > 0);
							case Op.AGreatOrEqualB:
								return ValNumber.Truth(string.Compare(sA, sB, StringComparison.Ordinal) >= 0);
							case Op.ALessThanB:
								int foo = string.Compare(sA, sB, StringComparison.Ordinal);
								return ValNumber.Truth(foo < 0);
							case Op.ALessOrEqualB:
								return ValNumber.Truth(string.Compare(sA, sB, StringComparison.Ordinal) <= 0);
							case Op.LengthOfA:
								return new ValNumber(sA.Length);
							default:
								break;
						}
					} else {
						// RHS is neither null nor a string.
						// We no longer automatically coerce in all these cases; about
						// all we can do is equal or unequal testing.
						// (Note that addition was handled way above here.)
						if (op == Op.AEqualB) return ValNumber.zero;
						if (op == Op.ANotEqualB) return ValNumber.one;						
					}
				} else if (opA is ValList) {
					List<Value> list = ((ValList)opA).values;
					if (op == Op.ElemBofA || op == Op.ElemBofIterA) {
						// list indexing
						return ((ValList)opA).GetElem(opB);
					} else if (op == Op.LengthOfA) {
						return new ValNumber(list.Count);
					} else if (op == Op.AEqualB) {
						return ValNumber.Truth(((ValList)opA).Equality(opB));
					} else if (op == Op.ANotEqualB) {
						return ValNumber.Truth(1 - ((ValList)opA).Equality(opB));
					} else if (op == Op.APlusB) {
						// list concatenation
						Check.Type(opB, typeof(ValList), "list concatenation");
						List<Value> list2 = ((ValList)opB).values;
						if (list.Count + list2.Count > ValList.maxSize) throw new LimitExceededException("list too large");
						List<Value> result = new List<Value>(list.Count + list2.Count);
						foreach (Value v in list) result.Add(context.ValueInContext(v));
						foreach (Value v in list2) result.Add(context.ValueInContext(v));
						return new ValList(result);
					} else if (op == Op.ATimesB || op == Op.ADividedByB) {
						// list replication (or division)
						FP factor = 0;
						if (op == Op.ATimesB) {
							Check.Type(opB, typeof(ValNumber), "list replication");
							factor = ((ValNumber)opB).value;
						} else {
							Check.Type(opB, typeof(ValNumber), "list division");
                            try {
							    factor = 1 / ((ValNumber)opB).value;								
                            } catch (DivideByZeroException) {
                                return null;
                            }
						}
						if (factor <= 0) return new ValList();
						int finalCount = (int)(list.Count * factor);
						if (finalCount > ValList.maxSize) throw new LimitExceededException("list too large");
						List<Value> result = new List<Value>(finalCount);
						for (int i = 0; i < finalCount; i++) {
							result.Add(context.ValueInContext(list[i % list.Count]));
						}
						return new ValList(result);
					} else if (op == Op.NotA) {
						return ValNumber.Truth(!opA.BoolValue());
					}
				} else if (opA is ValMap) {
					if (op == Op.ElemBofA) {
						// map lookup
						// (note, cases where opB is a string are handled above, along with
						// all the other types; so we'll only get here for non-string cases)
						ValSeqElem se = new ValSeqElem(opA, opB);
						return se.Val(context);
						// (This ensures we walk the "__isa" chain in the standard way.)
					} else if (op == Op.ElemBofIterA) {
						// With a map, ElemBofIterA is different from ElemBofA.  This one
						// returns a mini-map containing a key/value pair.
						return ((ValMap)opA).GetKeyValuePair(opB.IntValue());
					} else if (op == Op.LengthOfA) {
						return new ValNumber(((ValMap)opA).Count);
					} else if (op == Op.AEqualB) {
						return ValNumber.Truth(((ValMap)opA).Equality(opB));
					} else if (op == Op.ANotEqualB) {
						return ValNumber.Truth((FP) 1 - ((ValMap)opA).Equality(opB));
					} else if (op == Op.APlusB) {
						// map combination
						Dictionary<Value, Value> map = ((ValMap)opA).map;
						Check.Type(opB, typeof(ValMap), "map combination");
						Dictionary<Value, Value> map2 = ((ValMap)opB).map;
						ValMap result = new ValMap();
						foreach (KeyValuePair<Value, Value> kv in map) result.map[kv.Key] = context.ValueInContext(kv.Value);
						foreach (KeyValuePair<Value, Value> kv in map2) result.map[kv.Key] = context.ValueInContext(kv.Value);
						return result;
					} else if (op == Op.NotA) {
						return ValNumber.Truth(!opA.BoolValue());
					}
				} else if (opA is ValFunction && opB is ValFunction) {
					Function fA = ((ValFunction)opA).function;
					Function fB = ((ValFunction)opB).function;
					switch (op) {
					case Op.AEqualB:
						return ValNumber.Truth(fA == fB);
					case Op.ANotEqualB:
						return ValNumber.Truth(fA != fB);
					}
				} else {
					// opA is something else... perhaps null
					switch (op) {
					case Op.BindAssignA:
						{
							if (context.variables == null) context.variables = new ValMap();
							ValFunction valFunc = (ValFunction)opA;
                            return valFunc.BindAndCopy(context.variables);
						}
					case Op.NotA:
						return opA != null && opA.BoolValue() ? ValNumber.zero : ValNumber.one;
					case Op.ElemBofA:
						if (opA is null) {
							throw new TypeException("Null Reference Exception: can't index into null");
						} else {
							throw new TypeException("Type Exception: can't index into this type");
						}
					}
				}
				

				if (op == Op.AAndB || op == Op.AOrB) {
					// We already handled the case where opA was a number above;
					// this code handles the case where opA is something else.
					FP fA = opA != null && opA.BoolValue() ? 1 : 0;
					FP fB;
					if (opB is ValNumber) fB = ((ValNumber)opB).value;
					else fB = opB != null && opB.BoolValue() ? 1 : 0;
					FP result;
					if (op == Op.AAndB) {
						result = AbsClamp01(fA * fB);
					} else {
						result = AbsClamp01(fA + fB - fA * fB);
					}
					return new ValNumber(result);
				}
				return null;
			}

			static FP AbsClamp01(FP d) {
				if (d < 0) d = -d;
				if (d > 1) return 1;
				return d;
			}

		}
		
		/// <summary>
		/// TAC.Context keeps track of the runtime environment, including local 
		/// variables.  Context objects form a linked list via a "parent" reference,
		/// with a new context formed on each function call (this is known as the
		/// call stack).
		/// </summary>
		public class Context {
			public List<Line> code;			// TAC lines we're executing
			public int lineNum;				// next line to be executed
			public ValMap variables;		// local variables for this call frame
			public ValMap outerVars;        // variables of the context where this function was defined
			public Value self;				// value of self in this context
			public Stack<Value> args;		// pushed arguments for upcoming calls
			public Context parent;			// parent (calling) context
			public Value resultStorage;		// where to store the return value (in the calling context)
			public Machine vm;				// virtual machine
			public Intrinsic.Result partialResult;	// work-in-progress of our current intrinsic
			public int implicitResultCounter;	// how many times we have stored an implicit result

			public bool done {
				get { return lineNum >= code.Count; }
			}

			public Context root {
				get {
					Context c = this;
					while (c.parent != null) c = c.parent;
					return c;
				}
			}

			public Interpreter interpreter {
				get {
					if (vm == null || vm.interpreter == null) return null;
					return vm.interpreter.Target as Interpreter;
				}
			}

			List<Value> temps;			// values of temporaries; temps[0] is always return value

			public Context(List<Line> code) {
				this.code = code;
			}

			public void ClearCodeAndTemps() {
		 		code.Clear();
				lineNum = 0;
				if (temps != null) temps.Clear();
			}

			/// <summary>
			/// Reset this context to the first line of code, clearing out any 
			/// temporary variables, and optionally clearing out all variables.
			/// </summary>
			/// <param name="clearVariables">if true, clear our local variables</param>
			public void Reset(bool clearVariables=true) {
				lineNum = 0;
				temps = null;
				if (clearVariables) variables = new ValMap();
			}

			public void JumpToEnd() {
				lineNum = code.Count;
			}

			public void SetTemp(int tempNum, Value value) {
				// OFI: let each context record how many temps it will need, so we
				// can pre-allocate this list with that many and avoid having to
				// grow it later.  Also OFI: do lifetime analysis on these temps
				// and reuse ones we don't need anymore.
				if (temps == null) temps = new List<Value>();
				while (temps.Count <= tempNum) temps.Add(null);
				temps[tempNum] = value;
			}

			public Value GetTemp(int tempNum) {
				return temps == null ? null : temps[tempNum];
			}

			public Value GetTemp(int tempNum, Value defaultValue) {
				if (temps != null && tempNum < temps.Count) return temps[tempNum];
				return defaultValue;
			}

			public void SetVar(string identifier, Value value) {
				if (identifier == "globals" || identifier == "locals") {
					throw new RuntimeException("can't assign to " + identifier);
				}
				if (identifier == "self") self = value;
				if (variables == null) variables = new ValMap();
				if (variables.assignOverride == null || !variables.assignOverride(variables, new ValString(identifier), value)) {
					variables[identifier] = value;
				}
			}
			
			/// <summary>
			/// Get the value of a local variable ONLY -- does not check any other
			/// scopes, nor check for special built-in identifiers like "globals".
			/// Used mainly by host apps to easily look up an argument to an
			/// intrinsic function call by the parameter name.
			/// </summary>
			public Value GetLocal(string identifier, Value defaultValue=null) {
				Value result;
				if (variables != null && variables.TryGetValue(identifier, out result)) {
					return result;
				}
				return defaultValue;
			}
			
			public int GetLocalInt(string identifier, int defaultValue = 0) {
				Value result;
				if (variables != null && variables.TryGetValue(identifier, out result)) {
					if (result == null) return 0;	// variable found, but its value was null!
					return result.IntValue();
				}
				return defaultValue;
			}

			public bool GetLocalBool(string identifier, bool defaultValue = false) {
				Value result;
				if (variables != null && variables.TryGetValue(identifier, out result)) {
					if (result == null) return false;	// variable found, but its value was null!
					return result.BoolValue();
				}
				return defaultValue;
			}

			public FP GetLocalFloat(string identifier, FP defaultValue = default) {
				Value result;
				if (variables != null && variables.TryGetValue(identifier, out result)) {
					if (result == null) return 0;	// variable found, but its value was null!
					return result.FloatValue();
				}
				return defaultValue;
			}

			public FP GetLocalDouble(string identifier, FP defaultValue = default) {
				Value result;
				if (variables != null && variables.TryGetValue(identifier, out result)) {
					if (result == null) return 0;	// variable found, but its value was null!
					return result.DoubleValue();
				}
				return defaultValue;
			}

			public string GetLocalString(string identifier, string defaultValue = null) {
				Value result;
				if (variables != null && variables.TryGetValue(identifier, out result)) {
					if (result == null) return null;	// variable found, but its value was null!
					return result.ToString();
				}
				return defaultValue;
			}

			public SourceLoc GetSourceLoc() {
				if (lineNum < 0 || lineNum >= code.Count) return null;
				return code[lineNum].location;
			}
			
			/// <summary>
			/// Get the value of a variable available in this context (including
			/// locals, globals, and intrinsics).  Raise an exception if no such
			/// identifier can be found.
			/// </summary>
			/// <param name="identifier">name of identifier to look up</param>
			/// <param name="localOnly">if true, look in local scope only</param>
			/// <returns>value of that identifier</returns>
			public Value GetVar(string identifier, ValVar.LocalOnlyMode localOnly=ValVar.LocalOnlyMode.Off) {
				// check for special built-in identifiers 'locals', 'globals', etc.
				switch (identifier.Length)
				{
				case 4:
					if (identifier == "self") return self;
					break;
				case 5:
					if (identifier == "outer") {
						// return module variables, if we have them; else globals
						if (outerVars != null) return outerVars;
						if (root.variables == null) root.variables = new ValMap();
						return root.variables;
					}
					break;
				case 6:
					if (identifier == "locals") {
						if (variables == null) variables = new ValMap();
						return variables;
					}
					break;
				case 7:
					if (identifier == "globals") {
						if (root.variables == null) root.variables = new ValMap();
						return root.variables;
					}
					break;
				}
				
				// check for a local variable
				Value result;
				if (variables != null && variables.TryGetValue(identifier, out result)) {
					return result;
				}
				if (localOnly != ValVar.LocalOnlyMode.Off) {
					if (localOnly == ValVar.LocalOnlyMode.Strict) throw new UndefinedLocalException(identifier);
					else vm.standardOutput("Warning: assignment of unqualified local '" + identifier 
					 + "' based on nonlocal is deprecated " + code[lineNum].location, true);
				}

				// check for a module variable
				if (outerVars != null && outerVars.TryGetValue(identifier, out result)) {
					return result;
				}

				// OK, we don't have a local or module variable with that name.
				// Check the global scope (if that's not us already).
				if (parent != null) {
					Context globals = root;
					if (globals.variables != null && globals.variables.TryGetValue(identifier, out result)) {
						return result;
					}
				}

				// Finally, check intrinsics.
				Intrinsic intrinsic = Intrinsic.GetByName(identifier);
				if (intrinsic != null) return intrinsic.GetFunc();

				// No luck there either?  Undefined identifier.
				throw new UndefinedIdentifierException(identifier);
			}

			public void StoreValue(Value lhs, Value value) {
				if (lhs is ValTemp) {
					SetTemp(((ValTemp)lhs).tempNum, value);
				} else if (lhs is ValVar) {
					SetVar(((ValVar)lhs).identifier, value);
				} else if (lhs is ValSeqElem) {
					ValSeqElem seqElem = (ValSeqElem)lhs;
					Value seq = seqElem.sequence.Val(this);
					if (seq == null) throw new RuntimeException("can't set indexed element of null");
					if (!seq.CanSetElem()) {
						throw new RuntimeException("can't set an indexed element in this type");
					}
					Value index = seqElem.index;
					if (index is ValVar || index is ValSeqElem || 
						index is ValTemp) index = index.Val(this);
					seq.SetElem(index, value);
				} else {
					if (lhs != null) throw new RuntimeException("not an lvalue");
				}
			}
			
			public Value ValueInContext(Value value) {
				if (value == null) return null;
				return value.Val(this);
			}

			/// <summary>
			/// Store a parameter argument in preparation for an upcoming call
			/// (which should be executed in the context returned by NextCallContext).
			/// </summary>
			/// <param name="arg">Argument.</param>
			public void PushParamArgument(Value arg) {
				if (args == null) args = new Stack<Value>();
				if (args.Count > 255) throw new RuntimeException("Argument limit exceeded");
				args.Push(arg);				
			}

			/// <summary>
			/// Get a context for the next call, which includes any parameter arguments
			/// that have been set.
			/// </summary>
			/// <returns>The call context.</returns>
			/// <param name="func">Function to call.</param>
			/// <param name="argCount">How many arguments to pop off the stack.</param>
			/// <param name="gotSelf">Whether this method was called with dot syntax.</param> 
			/// <param name="resultStorage">Value to stuff the result into when done.</param>
			public Context NextCallContext(Function func, int argCount, bool gotSelf, Value resultStorage) {
				Context result = new Context(func.code);

				result.code = func.code;
				result.resultStorage = resultStorage;
				result.parent = this;
				result.vm = vm;

				// Stuff arguments, stored in our 'args' stack,
				// into local variables corrersponding to parameter names.
				// As a special case, skip over the first parameter if it is named 'self'
				// and we were invoked with dot syntax.
				int selfParam = (gotSelf && func.parameters.Count > 0 && func.parameters[0].name == "self" ? 1 : 0);
				for (int i = 0; i < argCount; i++) {
					// Careful -- when we pop them off, they're in reverse order.
					Value argument = args.Pop();
					int paramNum = argCount - 1 - i + selfParam;
					if (paramNum >= func.parameters.Count) {
						throw new TooManyArgumentsException();
					}
					string param = func.parameters[paramNum].name;
					if (param == "self") result.self = argument;
					else result.SetVar(param, argument);
				}
				// And fill in the rest with default values
				for (int paramNum = argCount+selfParam; paramNum < func.parameters.Count; paramNum++) {
					result.SetVar(func.parameters[paramNum].name, func.parameters[paramNum].defaultValue);
				}

				return result;
			}

			/// <summary>
			/// This function prints the three-address code to the console, for debugging purposes.
			/// </summary>
			public void Dump() {
				Console.WriteLine("CODE:");
				TAC.Dump(code, lineNum);

				Console.WriteLine("\nVARS:");
				if (variables == null) {
					Console.WriteLine(" NONE");
				} else {
					foreach (Value v in variables.Keys) {
						string id = v.ToString(vm);
						Console.WriteLine(string.Format("{0}: {1}", id, variables[id].ToString(vm)));
					}
				}

				Console.WriteLine("\nTEMPS:");
				if (temps == null) {
					Console.WriteLine(" NONE");
				} else {
					for (int i = 0; i < temps.Count; i++) {
						Console.WriteLine(string.Format("_{0}: {1}", i, temps[i]));
					}
				}
			}

			public override string ToString() {
				return string.Format("Context[{0}/{1}]", lineNum, code.Count);
			}
		}
		
		/// <summary>
		/// TAC.Machine implements a complete MiniScript virtual machine.  It 
		/// keeps the context stack, keeps track of run time, and provides 
		/// methods to step, stop, or reset the program.		
		/// </summary>
		public class Machine {
			public WeakReference interpreter;		// interpreter hosting this machine
			public TextOutputMethod standardOutput;	// where print() results should go
			public bool storeImplicit = false;		// whether to store implicit values (e.g. for REPL)
			public bool yielding = false;			// set to true by yield intrinsic
			public ValMap functionType;
			public ValMap listType;
			public ValMap mapType;
			public ValMap numberType;
			public ValMap stringType;
			public ValMap versionMap;
			
			public Context globalContext {			// contains global variables
				get { return _globalContext; }
			}

			public bool done {
				get { return (stack.Count <= 1 && stack.Peek().done); }
			}

			public double runTime {
				get { return stopwatch == null ? 0 : stopwatch.Elapsed.TotalSeconds; }
			}

			Context _globalContext;
			Stack<Context> stack;
			System.Diagnostics.Stopwatch stopwatch;

			public Machine(Context globalContext, TextOutputMethod standardOutput) {
				_globalContext = globalContext;
				_globalContext.vm = this;
				if (standardOutput == null) {
					this.standardOutput = (s,eol) => Console.WriteLine(s);
				} else {
					this.standardOutput = standardOutput;
				}
				stack = new Stack<Context>();
				stack.Push(_globalContext);
			}

			public void Stop() {
				while (stack.Count > 1) stack.Pop();
				stack.Peek().JumpToEnd();
			}
			
			public void Reset(bool clear = false) {
				while (stack.Count > 1) stack.Pop();
				stack.Peek().Reset(clear);
			}

			public void Step() {
				if (stack.Count == 0) return;		// not even a global context
				if (stopwatch == null) {
					stopwatch = new System.Diagnostics.Stopwatch();
					stopwatch.Start();
				}
				Context context = stack.Peek();
				while (context.done) {
					if (stack.Count == 1) return;	// all done (can't pop the global context)
					PopContext();
					context = stack.Peek();
				}

				Line line = context.code[context.lineNum++];
				try {
					DoOneLine(line, context);
				} catch (MiniscriptException mse) {
					if (mse.location == null) mse.location = line.location;
					if (mse.location == null) {
						foreach (Context c in stack) {
							if (c.lineNum >= c.code.Count) continue;
							mse.location = c.code[c.lineNum].location;
							if (mse.location != null) break;
						}
					}
					throw;
				}
			}
			
			/// <summary>
			/// Directly invoke a ValFunction by manually pushing it onto the call stack.
			/// This might be useful, for example, in invoking handlers that have somehow
			/// been registered with the host app via intrinsics.
			/// </summary>
			/// <param name="func">Miniscript function to invoke</param>
			/// <param name="resultStorage">where to store result of the call, in the calling context</param>
			/// <param name="arguments">optional list of arguments to push</param>
			public void ManuallyPushCall(ValFunction func, Value resultStorage=null, List<Value> arguments=null) {
				var context = stack.Peek();
				int argCount = func.function.parameters.Count;
				for (int i=0; i<argCount; i++) {
					if (arguments != null && i < arguments.Count) {
						Value val = context.ValueInContext(arguments[i]);
						context.PushParamArgument(val);						
					} else {
						context.PushParamArgument(null);
					}
				}
				Value self = null;	// "self" is always null for a manually pushed call
				
				Context nextContext = context.NextCallContext(func.function, argCount, self != null, null);
				if (self != null) nextContext.self = self;
				nextContext.resultStorage = resultStorage;
				stack.Push(nextContext);				
			}
			
			void DoOneLine(Line line, Context context) {
//				Console.WriteLine("EXECUTING line " + (context.lineNum-1) + ": " + line);
				if (line.op == Line.Op.PushParam) {
					Value val = context.ValueInContext(line.rhsA);
					context.PushParamArgument(val);
				} else if (line.op == Line.Op.CallFunctionA) {
					// Resolve rhsA.  If it's a function, invoke it; otherwise,
					// just store it directly (but pop the call context).
					ValMap valueFoundIn;
					Value funcVal = line.rhsA.Val(context, out valueFoundIn);	// resolves the whole dot chain, if any
					if (funcVal is ValFunction) {
						Value self = null;
						// bind "super" to the parent of the map the function was found in
						Value super = valueFoundIn == null ? null : valueFoundIn.Lookup(ValString.magicIsA);
						if (line.rhsA is ValSeqElem) {
							// bind "self" to the object used to invoke the call, except
							// when invoking via "super"
							Value seq = ((ValSeqElem)(line.rhsA)).sequence;
							if (seq is ValVar && ((ValVar)seq).identifier == "super") self = context.self;
							else self = context.ValueInContext(seq);
						}
						ValFunction func = (ValFunction)funcVal;
						int argCount = line.rhsB.IntValue();
						Context nextContext = context.NextCallContext(func.function, argCount, self != null, line.lhs);
						nextContext.outerVars = func.outerVars;
						if (valueFoundIn != null) nextContext.SetVar("super", super);
						if (self != null) nextContext.self = self;	// (set only if bound above)
						stack.Push(nextContext);
					} else {
						// The user is attempting to call something that's not a function.
						// We'll allow that, but any number of parameters is too many.  [#35]
						// (No need to pop them, as the exception will pop the whole call stack anyway.)
						int argCount = line.rhsB.IntValue();
						if (argCount > 0) throw new TooManyArgumentsException();
						context.StoreValue(line.lhs, funcVal);
					}
				} else if (line.op == Line.Op.ReturnA) {
					Value val = line.Evaluate(context);
					context.StoreValue(line.lhs, val);
					PopContext();
				} else if (line.op == Line.Op.AssignImplicit) {
					Value val = line.Evaluate(context);
					if (storeImplicit) {
						context.StoreValue(ValVar.implicitResult, val);
						context.implicitResultCounter++;
					}
				} else {
					Value val = line.Evaluate(context);
					context.StoreValue(line.lhs, val);
				}
			}

			void PopContext() {
				// Our top context is done; pop it off, and copy the return value in temp 0.
				if (stack.Count == 1) return;	// down to just the global stack (which we keep)
				Context context = stack.Pop();
				Value result = context.GetTemp(0, null);
				Value storage = context.resultStorage;
				context = stack.Peek();
				context.StoreValue(storage, result);
			}

			public Context GetTopContext() {
				return stack.Peek();
			}

			public void DumpTopContext() {
				stack.Peek().Dump();
			}
			
			public string FindShortName(Value val) {
				if (globalContext == null || globalContext.variables == null) return null;
				foreach (var kv in globalContext.variables.map) {
					if (kv.Value == val && kv.Key != val) return kv.Key.ToString(this);
				}
				string result = null;
				Intrinsic.shortNames.TryGetValue(val, out result);
				return result;
			}
			
			public List<SourceLoc> GetStack() {
				var result = new List<SourceLoc>();
				// NOTE: C# iteration over a Stack goes in reverse order.
				// This will return the newest call context first, and the
				// oldest (global) call context last.
				foreach (var context in stack) {
					result.Add(context.GetSourceLoc());
				}
				return result;
			}
		}

		public static void Dump(List<Line> lines, int lineNumToHighlight, int indent=0) {
			int lineNum = 0;
			foreach (Line line in lines) {
				string s = (lineNum == lineNumToHighlight ? "> " : "  ") + (lineNum++) + ". ";
				Console.WriteLine(s + line);
				if (line.op == Line.Op.BindAssignA) {
					ValFunction func = (ValFunction)line.rhsA;
					Dump(func.function.code, -1, indent+1);
				}
			}
		}

		public static ValTemp LTemp(int tempNum) {
			return new ValTemp(tempNum);
		}
		public static ValVar LVar(string identifier) {
			if (identifier == "self") return ValVar.self;
			return new ValVar(identifier);
		}
		public static ValTemp RTemp(int tempNum) {
			return new ValTemp(tempNum);
		}
		public static ValNumber Num(FP value) {
			return new ValNumber(value);
		}
		public static ValString Str(string value) {
			return new ValString(value);
		}
		public static ValNumber IntrinsicByName(string name) {
			return new ValNumber(Intrinsic.GetByName(name).id);
		}
		
	}
}

