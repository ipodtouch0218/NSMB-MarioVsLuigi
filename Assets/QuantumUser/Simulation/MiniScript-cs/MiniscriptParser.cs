/*	MiniscriptParser.cs

This file is responsible for parsing MiniScript source code, and converting
it into an internal format (a three-address byte code) that is considerably
faster to execute.

This is normally wrapped by the Interpreter class, so you probably don't
need to deal with Parser directly.
*/
using System;
using System.Collections.Generic;
using System.Linq;
using System.Globalization;
using Photon.Deterministic;

namespace Miniscript {
	public class Parser {

		public string errorContext;	// name of file, etc., used for error reporting
		//public int lineNum;			// which line number we're currently parsing

		// BackPatch: represents a place where we need to patch the code to fill
		// in a jump destination (once we figure out where that destination is).
		class BackPatch {
			public int lineNum;			// which code line to patch
			public string waitingFor;	// what keyword we're waiting for (e.g., "end if")
		}

		// JumpPoint: represents a place in the code we will need to jump to later
		// (typically, the top of a loop of some sort).
		class JumpPoint {
			public int lineNum;			// line number to jump to		
			public string keyword;		// jump type, by keyword: "while", "for", etc.
		}

		class ParseState {
			public List<TAC.Line> code = new List<TAC.Line>();
			public List<BackPatch> backpatches = new List<BackPatch>();
			public List<JumpPoint> jumpPoints = new List<JumpPoint>();
			public int nextTempNum = 0;
			public string localOnlyIdentifier;	// identifier to be looked up in local scope *only*
			public bool localOnlyStrict;		// whether localOnlyIdentifier applies strictly, or merely warns
			
			public void Add(TAC.Line line) {
				code.Add(line);
			}

			/// <summary>
			/// Add the last code line as a backpatch point, to be patched
			/// (in rhsA) when we encounter a line with the given waitFor.
			/// </summary>
			/// <param name="waitFor">Wait for.</param>
			public void AddBackpatch(string waitFor) {
				backpatches.Add(new BackPatch() { lineNum=code.Count-1, waitingFor=waitFor });
			}

			public void AddJumpPoint(string jumpKeyword) {
				jumpPoints.Add(new JumpPoint() { lineNum = code.Count, keyword = jumpKeyword });
			}

			public JumpPoint CloseJumpPoint(string keyword) {
				int idx = jumpPoints.Count - 1;
				if (idx < 0 || jumpPoints[idx].keyword != keyword) {
					throw new CompilerException(string.Format("'end {0}' without matching '{0}'", keyword));
				}
				JumpPoint result = jumpPoints[idx];
				jumpPoints.RemoveAt(idx);
				return result;
			}

			// Return whether the given line is a jump target.
			public bool IsJumpTarget(int lineNum) {
				for (int i=0; i < code.Count; i++) {
					var op = code[i].op;
					if ((op == TAC.Line.Op.GotoA || op == TAC.Line.Op.GotoAifB 
					 || op == TAC.Line.Op.GotoAifNotB || op == TAC.Line.Op.GotoAifTrulyB)
					 && code[i].rhsA is ValNumber && code[i].rhsA.IntValue() == lineNum) return true;
				}
				for (int i=0; i<jumpPoints.Count(); i++) {
					if (jumpPoints[i].lineNum == lineNum) return true;
				}
				return false;
			}

			/// <summary>
			/// Call this method when we've found an 'end' keyword, and want
			/// to patch up any jumps that were waiting for that.  Patch the
			/// matching backpatch (and any after it) to the current code end.
			/// </summary>
			/// <param name="keywordFound">Keyword found.</param>
			/// <param name="reservingLines">Extra lines (after the current position) to patch to.</param> 
			public void Patch(string keywordFound, int reservingLines=0) {
				Patch(keywordFound, false, reservingLines);
			}

			/// <summary>
			/// Call this method when we've found an 'end' keyword, and want
			/// to patch up any jumps that were waiting for that.  Patch the
			/// matching backpatch (and any after it) to the current code end.
			/// </summary>
			/// <param name="keywordFound">Keyword found.</param>
			/// <param name="alsoBreak">If true, also patch "break"; otherwise skip it.</param> 
			/// <param name="reservingLines">Extra lines (after the current position) to patch to.</param> 
			public void Patch(string keywordFound, bool alsoBreak, int reservingLines=0) {
				Value target = TAC.Num(code.Count + reservingLines);
				bool done = false;
				for (int idx = backpatches.Count - 1; idx >= 0 && !done; idx--) {
					bool patchIt = false;
					if (backpatches[idx].waitingFor == keywordFound) patchIt = done = true;
					else if (backpatches[idx].waitingFor == "break") {
						// Not the expected keyword, but "break"; this is always OK,
						// but we may or may not patch it depending on the call.
						patchIt = alsoBreak;
					} else {
						// Not the expected patch, and not "break"; we have a mismatched block start/end.
						throw new CompilerException("'" + keywordFound + "' skips expected '" + backpatches[idx].waitingFor + "'");
					}
					if (patchIt) {
						code[backpatches[idx].lineNum].rhsA = target;
						backpatches.RemoveAt(idx);
					}
				}
				// Make sure we found one...
				if (!done) throw new CompilerException("'" + keywordFound + "' without matching block starter");
			}

			/// <summary>
			/// Patches up all the branches for a single open if block.  That includes
			/// the last "else" block, as well as one or more "end if" jumps.
			/// </summary>
			public void PatchIfBlock(bool singleLineIf) {
				Value target = TAC.Num(code.Count);

				int idx = backpatches.Count - 1;
				while (idx >= 0) {
					BackPatch bp = backpatches[idx];
					if (bp.waitingFor == "if:MARK") {
						// There's the special marker that indicates the true start of this if block.
						backpatches.RemoveAt(idx);
						return;
					} else if (bp.waitingFor == "end if" || bp.waitingFor == "else") {
						code[bp.lineNum].rhsA = target;
						backpatches.RemoveAt(idx);
					} else if (backpatches[idx].waitingFor == "break") {
						// Not the expected keyword, but "break"; this is always OK.
					} else {
						// Not the expected patch, and not "break"; we have a mismatched block start/end.
						string msg;
						if (singleLineIf) {
							if (bp.waitingFor == "end for" || bp.waitingFor == "end while") {
								msg = "loop is invalid within single-line 'if'";
							} else {
								msg = "invalid control structure within single-line 'if'";
							}
						} else {
							msg = "'end if' without matching 'if'";
						}
						throw new CompilerException(msg);
					}
					idx--;
				}
				// If we get here, we never found the expected if:MARK.  That's an error.
				throw new CompilerException("'end if' without matching 'if'");
			}
		}
		
		// Partial input, in the case where line continuation has been used.
		string partialInput;

		// List of open code blocks we're working on (while compiling a function,
		// we push a new one onto this stack, compile to that, and then pop it
		// off when we reach the end of the function).
		Stack<ParseState> outputStack;

		// Handy reference to the top of outputStack.
		ParseState output;

		// A new parse state that needs to be pushed onto the stack, as soon as we
		// finish with the current line we're working on:
		ParseState pendingState = null;

		public Parser() {
			Reset();
		}

		/// <summary>
		/// Completely clear out and reset our parse state, throwing out
		/// any code and intermediate results.
		/// </summary>
		public void Reset() {
			output = new ParseState();
			if (outputStack == null) outputStack = new Stack<ParseState>();
			else outputStack.Clear();
			outputStack.Push(output);
		}

		/// <summary>
		/// Partially reset, abandoning backpatches, but keeping already-
		/// compiled code.  This would be used in a REPL, when the user
		/// may want to reset and continue after a botched loop or function.
		/// </summary>
		public void PartialReset() {
			if (outputStack == null) outputStack = new Stack<ParseState>();
			while (outputStack.Count > 1) outputStack.Pop();
			output = outputStack.Peek();
			output.backpatches.Clear();
			output.jumpPoints.Clear();
			output.nextTempNum = 0;
			partialInput = null;
			pendingState = null;
		}

		public bool NeedMoreInput() {
			if (!string.IsNullOrEmpty(partialInput)) return true;
			if (outputStack.Count > 1) return true;
			if (output.backpatches.Count > 0) return true;
			return false;
		}

		/// <summary>
		/// Return whether the given source code ends in a token that signifies that
		/// the statement continues on the next line.  That includes binary operators,
		/// open brackets or parentheses, etc.
		/// </summary>
		/// <param name="sourceCode">source code to analyze</param>
		/// <returns>true if line continuation is called for; false otherwise</returns>
		public static bool EndsWithLineContinuation(string sourceCode) {
 			try {
				Token lastTok = Lexer.LastToken(sourceCode);
				// Almost any token at the end will signify line continuation, except:
				switch (lastTok.type) {
				case Token.Type.EOL:
				case Token.Type.Identifier:
				case Token.Type.Number:
				case Token.Type.RCurly:
				case Token.Type.RParen:
				case Token.Type.RSquare:
				case Token.Type.String:
				case Token.Type.Unknown:
					return false;
				case Token.Type.Keyword:
					// of keywords, only these can cause line continuation:
					return lastTok.text == "and" || lastTok.text == "or" || lastTok.text == "isa"
							|| lastTok.text == "not" || lastTok.text == "new";
				default:
					return true;
				}
			} catch (LexerException) {
				return false;
			}
		}

		void CheckForOpenBackpatches(int sourceLineNum) {
			if (output.backpatches.Count == 0) return;
			BackPatch bp = output.backpatches[output.backpatches.Count - 1];
			string msg;
			switch (bp.waitingFor) {
			case "end for":
				msg = "'for' without matching 'end for'";
				break;
			case "end if":
			case "else":
				msg = "'if' without matching 'end if'";
				break;
			case "end while":
				msg = "'while' without matching 'end while'";
				break;
			default:
				msg = "unmatched block opener";
				break;
			}
			throw new CompilerException(errorContext, sourceLineNum, msg);
		}

		public void Parse(string sourceCode, bool replMode=false) {
			if (replMode) {
				// Check for an incomplete final line by finding the last (non-comment) token.
				bool isPartial = EndsWithLineContinuation(sourceCode);
				if (isPartial) {
					partialInput += Lexer.TrimComment(sourceCode);
					partialInput += " ";
					return;
				}
			}
			Lexer tokens = new Lexer(partialInput + sourceCode);
			partialInput = null;
			ParseMultipleLines(tokens);

			if (!replMode && NeedMoreInput()) {
				// Whoops, we need more input but we don't have any.  This is an error.
				tokens.lineNum++;	// (so we report PAST the last line, making it clear this is an EOF problem)
				if (outputStack.Count > 1) {
					throw new CompilerException(errorContext, tokens.lineNum,
						"'function' without matching 'end function'");
				} 
				CheckForOpenBackpatches(tokens.lineNum);
			}
		}

		/// <summary>
		/// Create a virtual machine loaded with the code we have parsed.
		/// </summary>
		/// <param name="standardOutput"></param>
		/// <returns></returns>
		public TAC.Machine CreateVM(TextOutputMethod standardOutput) {
			TAC.Context root = new TAC.Context(output.code);
			return new TAC.Machine(root, standardOutput);
		}
		
		/// <summary>
		/// Create a Function with the code we have parsed, for use as
		/// an import.  That means, it runs all that code, then at the
		/// end it returns `locals` so that the caller can get its symbols.
		/// </summary>
		/// <returns></returns>
		public Function CreateImport() {
			// Add one additional line to return `locals` as the function return value.
			ValVar locals = new ValVar("locals");
			output.Add(new TAC.Line(TAC.LTemp(0), TAC.Line.Op.ReturnA, locals));
			// Then wrap the whole thing in a Function.
			var result = new Function(output.code);
			return result;
		}

		public void REPL(string line) {
			Parse(line);
		
			TAC.Machine vm = CreateVM(null);
			while (!vm.done) vm.Step();
		}

		void AllowLineBreak(Lexer tokens) {
			while (tokens.Peek().type == Token.Type.EOL && !tokens.AtEnd) tokens.Dequeue();
		}

		delegate Value ExpressionParsingMethod(Lexer tokens, bool asLval=false, bool statementStart=false);

		/// <summary>
		/// Parse multiple statements until we run out of tokens, or reach 'end function'.
		/// </summary>
		/// <param name="tokens">Tokens.</param>
		void ParseMultipleLines(Lexer tokens) {
			while (!tokens.AtEnd) {
				// Skip any blank lines
				if (tokens.Peek().type == Token.Type.EOL) {
					tokens.Dequeue();
					continue;
				}

				// Prepare a source code location for error reporting
				SourceLoc location = new SourceLoc(errorContext, tokens.lineNum);

				// Pop our context if we reach 'end function'.
				if (tokens.Peek().type == Token.Type.Keyword && tokens.Peek().text == "end function") {
					tokens.Dequeue();
					if (outputStack.Count > 1) {
						CheckForOpenBackpatches(tokens.lineNum);
						outputStack.Pop();
						output = outputStack.Peek();
					} else {
						CompilerException e = new CompilerException("'end function' without matching block starter");
						e.location = location;
						throw e;
					}
					continue;
				}

				// Parse one line (statement).
				int outputStart = output.code.Count;
				try {
					ParseStatement(tokens);
				} catch (MiniscriptException mse) {
					if (mse.location == null) mse.location = location;
					throw;
				}
				// Fill in the location info for all the TAC lines we just generated.
				for (int i = outputStart; i < output.code.Count; i++) {
					output.code[i].location = location;
				}
			}
		}

		void ParseStatement(Lexer tokens, bool allowExtra=false) {
			if (tokens.Peek().type == Token.Type.Keyword && tokens.Peek().text != "not"
				&& tokens.Peek().text != "true" && tokens.Peek().text != "false") {
				// Handle statements that begin with a keyword.
				string keyword = tokens.Dequeue().text;
				switch (keyword) {
				case "return":
					{
						Value returnValue = null;
						if (tokens.Peek().type != Token.Type.EOL && tokens.Peek().text != "else" && tokens.Peek().text != "else if") {
							returnValue = ParseExpr(tokens);
						}
						output.Add(new TAC.Line(TAC.LTemp(0), TAC.Line.Op.ReturnA, returnValue));
					}
					break;
				case "if":
					{
						Value condition = ParseExpr(tokens);
						RequireToken(tokens, Token.Type.Keyword, "then");
						// OK, now we need to emit a conditional branch, but keep track of this
						// on a stack so that when we get the corresponding "else" or  "end if", 
						// we can come back and patch that jump to the right place.
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoAifNotB, null, condition));

						// ...but if blocks also need a special marker in the backpack stack
						// so we know where to stop when patching up (possibly multiple) 'end if' jumps.
						// We'll push a special dummy backpatch here that we look for in PatchIfBlock.
						output.AddBackpatch("if:MARK");
						output.AddBackpatch("else");
						
						// Allow for the special one-statement if: if the next token after "then"
						// is not EOL, then parse a statement, and do the same for any else or
						// else-if blocks, until we get to EOL (and then implicitly do "end if").
						if (tokens.Peek().type != Token.Type.EOL) {
							ParseStatement(tokens, true);  // parses a single statement for the "then" body
							if (tokens.Peek().type == Token.Type.Keyword && tokens.Peek().text == "else") {
								tokens.Dequeue();	// skip "else"
								StartElseClause();
								ParseStatement(tokens, true);		// parse a single statement for the "else" body
							} else if (tokens.Peek().type == Token.Type.Keyword && tokens.Peek().text == "else if") {
								tokens.Peek().text = "if";		// the trick: convert the "else if" token to a regular "if"...
								StartElseClause();				// but start an else clause...
								ParseStatement(tokens, true);	// then parse a single statement starting with "if"
							} else {
								RequireEitherToken(tokens, Token.Type.Keyword, "else", Token.Type.EOL);
							}
							output.PatchIfBlock(true);	// terminate the single-line if
						} else {
							tokens.Dequeue();	// skip EOL
						}
					}
					return;
				case "else":
					StartElseClause();
					break;
				case "else if":
					{
						StartElseClause();
						Value condition = ParseExpr(tokens);
						RequireToken(tokens, Token.Type.Keyword, "then");
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoAifNotB, null, condition));
						output.AddBackpatch("else");
					}
					break;
				case "end if":
					// OK, this is tricky.  We might have an open "else" block or we might not.
					// And, we might have multiple open "end if" jumps (one for the if part,
					// and another for each else-if part).  Patch all that as a special case.
					output.PatchIfBlock(false);
					break;
				case "while":
					{
						// We need to note the current line, so we can jump back up to it at the end.
						output.AddJumpPoint(keyword);

						// Then parse the condition.
						Value condition = ParseExpr(tokens);

						// OK, now we need to emit a conditional branch, but keep track of this
						// on a stack so that when we get the corresponding "end while", 
						// we can come back and patch that jump to the right place.
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoAifNotB, null, condition));
						output.AddBackpatch("end while");
					}
					break;
				case "end while":
					{
						// Unconditional jump back to the top of the while loop.
						JumpPoint jump = output.CloseJumpPoint("while");
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(jump.lineNum)));
						// Then, backpatch the open "while" branch to here, right after the loop.
						// And also patch any "break" branches emitted after that point.
						output.Patch(keyword, true);
					}
					break;
				case "for":
					{
						// Get the loop variable, "in" keyword, and expression to loop over.
						// (Note that the expression is only evaluated once, before the loop.)
						Token loopVarTok = RequireToken(tokens, Token.Type.Identifier);
						ValVar loopVar = new ValVar(loopVarTok.text);
						RequireToken(tokens, Token.Type.Keyword, "in");
						Value stuff = ParseExpr(tokens);
						if (stuff == null) {
							throw new CompilerException(errorContext, tokens.lineNum,
								"sequence expression expected for 'for' loop");
						}

						// Create an index variable to iterate over the sequence, initialized to -1.
						ValVar idxVar = new ValVar("__" + loopVarTok.text + "_idx");
						output.Add(new TAC.Line(idxVar, TAC.Line.Op.AssignA, TAC.Num(-1)));

						// We need to note the current line, so we can jump back up to it at the end.
						output.AddJumpPoint(keyword);

						// Now increment the index variable, and branch to the end if it's too big.
						// (We'll have to backpatch this branch later.)
						output.Add(new TAC.Line(idxVar, TAC.Line.Op.APlusB, idxVar, TAC.Num(1)));
						ValTemp sizeOfSeq = new ValTemp(output.nextTempNum++);
						output.Add(new TAC.Line(sizeOfSeq, TAC.Line.Op.LengthOfA, stuff));
						ValTemp isTooBig = new ValTemp(output.nextTempNum++);
						output.Add(new TAC.Line(isTooBig, TAC.Line.Op.AGreatOrEqualB, idxVar, sizeOfSeq));
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoAifB, null, isTooBig));
						output.AddBackpatch("end for");

						// Otherwise, get the sequence value into our loop variable.
						output.Add(new TAC.Line(loopVar, TAC.Line.Op.ElemBofIterA, stuff, idxVar));
					}
					break;
				case "end for":
					{
						// Unconditional jump back to the top of the for loop.
						JumpPoint jump = output.CloseJumpPoint("for");
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(jump.lineNum)));
						// Then, backpatch the open "for" branch to here, right after the loop.
						// And also patch any "break" branches emitted after that point.
						output.Patch(keyword, true);
					}
					break;
				case "break":
					{
						// Emit a jump to the end, to get patched up later.
						if (output.jumpPoints.Count == 0) {
							throw new CompilerException(errorContext, tokens.lineNum,
								"'break' without open loop block");
						}
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA));
						output.AddBackpatch("break");
					}
					break;
				case "continue":
					{
						// Jump unconditionally back to the current open jump point.
						if (output.jumpPoints.Count == 0) {
							throw new CompilerException(errorContext, tokens.lineNum,
								"'continue' without open loop block");
						}
						JumpPoint jump = output.jumpPoints.Last();
						output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(jump.lineNum)));
					}
					break;
				default:
					throw new CompilerException(errorContext, tokens.lineNum,
						"unexpected keyword '" + keyword + "' at start of line");
				}
			} else {
				ParseAssignment(tokens, allowExtra);
			}

			// A statement should consume everything to the end of the line.
			if (!allowExtra) RequireToken(tokens, Token.Type.EOL);

			// Finally, if we have a pending state, because we encountered a function(),
			// then push it onto our stack now that we're done with that statement.
			if (pendingState != null) {
				output = pendingState;
				outputStack.Push(output);
				pendingState = null;
			}

		}
		
		void StartElseClause() {
			// Back-patch the open if block, but leaving room for the jump:
			// Emit the jump from the current location, which is the end of an if-block,
			// to the end of the else block (which we'll have to back-patch later).
			output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, null));
			// Back-patch the previously open if-block to jump here (right past the goto).
			output.Patch("else");
			// And open a new back-patch for this goto (which will jump all the way to the end if).
			output.AddBackpatch("end if");
		}

		void ParseAssignment(Lexer tokens, bool allowExtra=false) {
			Value expr = ParseExpr(tokens, true, true);
			Value lhs, rhs;
			Token peek = tokens.Peek();
			if (peek.type == Token.Type.EOL ||
					(peek.type == Token.Type.Keyword && (peek.text == "else" || peek.text == "else if"))) {
				// No explicit assignment; store an implicit result
				rhs = FullyEvaluate(expr);
				output.Add(new TAC.Line(null, TAC.Line.Op.AssignImplicit, rhs));
				return;
			}
			if (peek.type == Token.Type.OpAssign) {
				tokens.Dequeue();	// skip '='
				lhs = expr;
				output.localOnlyIdentifier = null;
				output.localOnlyStrict = false;	// ToDo: make this always strict, and change "localOnly" to a simple bool
				if (lhs is ValVar vv) output.localOnlyIdentifier = vv.identifier;
				rhs = ParseExpr(tokens);
				output.localOnlyIdentifier = null;
			} else if (peek.type == Token.Type.OpAssignPlus || peek.type == Token.Type.OpAssignMinus
				    || peek.type == Token.Type.OpAssignTimes || peek.type == Token.Type.OpAssignDivide
				    || peek.type == Token.Type.OpAssignMod || peek.type == Token.Type.OpAssignPower) {
				var op = TAC.Line.Op.APlusB;
				switch (tokens.Dequeue().type) {
				case Token.Type.OpAssignMinus:		op = TAC.Line.Op.AMinusB;		break;
				case Token.Type.OpAssignTimes:		op = TAC.Line.Op.ATimesB;		break;
				case Token.Type.OpAssignDivide:		op = TAC.Line.Op.ADividedByB;	break;
				case Token.Type.OpAssignMod:		op = TAC.Line.Op.AModB;			break;
				case Token.Type.OpAssignPower:		op = TAC.Line.Op.APowB;			break;
				default: break;
				}

				lhs = expr;
				output.localOnlyIdentifier = null;
				output.localOnlyStrict = true;
				if (lhs is ValVar vv) output.localOnlyIdentifier = vv.identifier;
				rhs = ParseExpr(tokens);
				
				var opA = FullyEvaluate(lhs, ValVar.LocalOnlyMode.Strict);
				Value opB = FullyEvaluate(rhs);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), op, opA, opB));
				rhs = TAC.RTemp(tempNum);
				output.localOnlyIdentifier = null;
			} else {
				// This looks like a command statement.  Parse the rest
				// of the line as arguments to a function call.
				Value funcRef = expr;
				int argCount = 0;
				while (true) {
					Value arg = ParseExpr(tokens);
					output.Add(new TAC.Line(null, TAC.Line.Op.PushParam, arg));
					argCount++;
					if (tokens.Peek().type == Token.Type.EOL) break;
					if (tokens.Peek().type == Token.Type.Keyword && (tokens.Peek().text == "else" || tokens.Peek().text == "else if")) break;
					if (tokens.Peek().type == Token.Type.Comma) {
						tokens.Dequeue();
						AllowLineBreak(tokens);
						continue;
					}
					if (RequireEitherToken(tokens, Token.Type.Comma, Token.Type.EOL).type == Token.Type.EOL) break;
				}
				ValTemp result = new ValTemp(output.nextTempNum++);
				output.Add(new TAC.Line(result, TAC.Line.Op.CallFunctionA, funcRef, TAC.Num(argCount)));					
				output.Add(new TAC.Line(null, TAC.Line.Op.AssignImplicit, result));
				return;
			}

			// Now we need to assign the value in rhs to the lvalue in lhs.
			// First, check for the case where lhs is a temp; that indicates it is not an lvalue
			// (for example, it might be a list slice).
			if (lhs is ValTemp) {
				throw new CompilerException(errorContext, tokens.lineNum, "invalid assignment (not an lvalue)");
			}

			// OK, now, in many cases our last TAC line at this point is an assignment to our RHS temp.
			// In that case, as a simple (but very useful) optimization, we can simply patch that to 
			// assign to our lhs instead.  BUT, we must not do this if there are any jumps to the next
			// line, as may happen due to short-cut evaluation (issue #6).
			if (rhs is ValTemp && output.code.Count > 0 && !output.IsJumpTarget(output.code.Count)) {			
				TAC.Line line = output.code[output.code.Count - 1];
				if (line.lhs.Equals(rhs)) {
					// Yep, that's the case.  Patch it up.
					line.lhs = lhs;
					return;
				}
			}
			
            // If the last line was us creating and assigning a function, then we don't add a second assign
            // op, we instead just update that line with the proper LHS
            if (rhs is ValFunction && output.code.Count > 0) {
                TAC.Line line = output.code[output.code.Count - 1];
                if (line.op == TAC.Line.Op.BindAssignA) {
                    line.lhs = lhs;
                    return;
                }
            }

			// In any other case, do an assignment statement to our lhs.
			output.Add(new TAC.Line(lhs, TAC.Line.Op.AssignA, rhs));
		}

		Value ParseExpr(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseFunction;
			return nextLevel(tokens, asLval, statementStart);
		}

		Value ParseFunction(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseOr;
			Token tok = tokens.Peek();
			if (tok.type != Token.Type.Keyword || tok.text != "function") return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();

			Function func = new Function(null);
			tok = tokens.Peek();
			if (tok.type != Token.Type.EOL) { 
				var paren = RequireToken(tokens, Token.Type.LParen);
				while (tokens.Peek().type != Token.Type.RParen) {
					// parse a parameter: a comma-separated list of
					//			identifier
					//	or...	identifier = constant
					Token id = tokens.Dequeue();
					if (id.type != Token.Type.Identifier) throw new CompilerException(errorContext, tokens.lineNum,
						"got " + id + " where an identifier is required");
					Value defaultValue = null;
					if (tokens.Peek().type == Token.Type.OpAssign) {
						tokens.Dequeue();	// skip '='
						defaultValue = ParseExpr(tokens);
						// Ensure the default value is a constant, not an expression.
						if (defaultValue is ValTemp) {
							throw new CompilerException(errorContext, tokens.lineNum,
								"parameter default value must be a literal value");
						}
					}
					func.parameters.Add(new Function.Param(id.text, defaultValue));
					if (tokens.Peek().type == Token.Type.RParen) break;
					RequireToken(tokens, Token.Type.Comma);
				}

				RequireToken(tokens, Token.Type.RParen);
			}

			// Now, we need to parse the function body into its own parsing context.
			// But don't push it yet -- we're in the middle of parsing some expression
			// or statement in the current context, and need to finish that.
			if (pendingState != null) throw new CompilerException(errorContext, tokens.lineNum,
				"can't start two functions in one statement");
			pendingState = new ParseState();
			pendingState.nextTempNum = 1;	// (since 0 is used to hold return value)

//			Console.WriteLine("STARTED FUNCTION");

			// Create a function object attached to the new parse state code.
			func.code = pendingState.code;
			var valFunc = new ValFunction(func);
			output.Add(new TAC.Line(null, TAC.Line.Op.BindAssignA, valFunc));
			return valFunc;
		}

		Value ParseOr(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseAnd;
			Value val = nextLevel(tokens, asLval, statementStart);
			List<TAC.Line> jumpLines = null;
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.Keyword && tok.text == "or") {
				tokens.Dequeue();		// discard "or"
				val = FullyEvaluate(val);

				AllowLineBreak(tokens); // allow a line break after a binary operator

				// Set up a short-circuit jump based on the current value; 
				// we'll fill in the jump destination later.  Note that the
				// usual GotoAifB opcode won't work here, without breaking
				// our calculation of intermediate truth.  We need to jump
				// only if our truth value is >= 1 (i.e. absolutely true).
				TAC.Line jump = new TAC.Line(null, TAC.Line.Op.GotoAifTrulyB, null, val);
				output.Add(jump);
				if (jumpLines == null) jumpLines = new List<TAC.Line>();
				jumpLines.Add(jump);

				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AOrB, val, opB));
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}

			// Now, if we have any short-circuit jumps, those are going to need
			// to copy the short-circuit result (always 1) to our output temp.
			// And anything else needs to skip over that.  So:
			if (jumpLines != null) {
				output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(output.code.Count+2)));	// skip over this line:
				output.Add(new TAC.Line(val, TAC.Line.Op.AssignA, ValNumber.one));	// result = 1
				foreach (TAC.Line jump in jumpLines) {
					jump.rhsA = TAC.Num(output.code.Count-1);	// short-circuit to the above result=1 line
				}
			}

			return val;
		}

		Value ParseAnd(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseNot;
			Value val = nextLevel(tokens, asLval, statementStart);
			List<TAC.Line> jumpLines = null;
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.Keyword && tok.text == "and") {
				tokens.Dequeue();		// discard "and"
				val = FullyEvaluate(val);

				AllowLineBreak(tokens); // allow a line break after a binary operator

				// Set up a short-circuit jump based on the current value; 
				// we'll fill in the jump destination later.
				TAC.Line jump = new TAC.Line(null, TAC.Line.Op.GotoAifNotB, null, val);
				output.Add(jump);
				if (jumpLines == null) jumpLines = new List<TAC.Line>();
				jumpLines.Add(jump);

				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AAndB, val, opB));
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}

			// Now, if we have any short-circuit jumps, those are going to need
			// to copy the short-circuit result (always 0) to our output temp.
			// And anything else needs to skip over that.  So:
			if (jumpLines != null) {
				output.Add(new TAC.Line(null, TAC.Line.Op.GotoA, TAC.Num(output.code.Count+2)));	// skip over this line:
				output.Add(new TAC.Line(val, TAC.Line.Op.AssignA, ValNumber.zero));	// result = 0
				foreach (TAC.Line jump in jumpLines) {
					jump.rhsA = TAC.Num(output.code.Count-1);	// short-circuit to the above result=0 line
				}
			}

			return val;
		}

		Value ParseNot(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseIsA;
			Token tok = tokens.Peek();
			Value val;
			if (tok.type == Token.Type.Keyword && tok.text == "not") {
				tokens.Dequeue();		// discard "not"

				AllowLineBreak(tokens); // allow a line break after a unary operator

				val = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.NotA, val));
				val = TAC.RTemp(tempNum);
			} else {
				val = nextLevel(tokens, asLval, statementStart
				);
			}
			return val;
		}

		Value ParseIsA(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseComparisons;
			Value val = nextLevel(tokens, asLval, statementStart);
			if (tokens.Peek().type == Token.Type.Keyword && tokens.Peek().text == "isa") {
				tokens.Dequeue();		// discard the isa operator
				AllowLineBreak(tokens); // allow a line break after a binary operator
				val = FullyEvaluate(val);
				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AisaB, val, opB));
				val = TAC.RTemp(tempNum);
			}
			return val;
		}
		
		Value ParseComparisons(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseAddSub;
			Value val = nextLevel(tokens, asLval, statementStart);
			Value opA = val;
			TAC.Line.Op opcode = ComparisonOp(tokens.Peek().type);
			// Parse a string of comparisons, all multiplied together
			// (so every comparison must be true for the whole expression to be true).
			bool firstComparison = true;
			while (opcode != TAC.Line.Op.Noop) {
				tokens.Dequeue();	// discard the operator (we have the opcode)
				opA = FullyEvaluate(opA);

				AllowLineBreak(tokens); // allow a line break after a binary operator

				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), opcode,	opA, opB));
				if (firstComparison) {
					firstComparison = false;
				} else {
					tempNum = output.nextTempNum++;
					output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.ATimesB, val, TAC.RTemp(tempNum - 1)));
				}
				val = TAC.RTemp(tempNum);
				opA = opB;
				opcode = ComparisonOp(tokens.Peek().type);
			}
			return val;
		}

		// Find the TAC operator that corresponds to the given token type,
		// for comparisons.  If it's not a comparison operator, return TAC.Line.Op.Noop.
		static TAC.Line.Op ComparisonOp(Token.Type tokenType) {
			switch (tokenType) {
			case Token.Type.OpEqual:		return TAC.Line.Op.AEqualB;
			case Token.Type.OpNotEqual:		return TAC.Line.Op.ANotEqualB;
			case Token.Type.OpGreater:		return TAC.Line.Op.AGreaterThanB;
			case Token.Type.OpGreatEqual:	return TAC.Line.Op.AGreatOrEqualB;
			case Token.Type.OpLesser:		return TAC.Line.Op.ALessThanB;
			case Token.Type.OpLessEqual:	return TAC.Line.Op.ALessOrEqualB;
			default: return TAC.Line.Op.Noop;
			}
		}

		Value ParseAddSub(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseMultDiv;
			Value val = nextLevel(tokens, asLval, statementStart);
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.OpPlus || 
					(tok.type == Token.Type.OpMinus
					&& (!statementStart || !tok.afterSpace  || tokens.IsAtWhitespace()))) {
				tokens.Dequeue();

				AllowLineBreak(tokens); // allow a line break after a binary operator

				val = FullyEvaluate(val);
				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), 
					tok.type == Token.Type.OpPlus ? TAC.Line.Op.APlusB : TAC.Line.Op.AMinusB,
					val, opB));
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}
			return val;
		}

		Value ParseMultDiv(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseUnaryMinus;
			Value val = nextLevel(tokens, asLval, statementStart);
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.OpTimes || tok.type == Token.Type.OpDivide || tok.type == Token.Type.OpMod) {
				tokens.Dequeue();

				AllowLineBreak(tokens); // allow a line break after a binary operator

				val = FullyEvaluate(val);
				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				switch (tok.type) {
				case Token.Type.OpTimes:
					output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.ATimesB, val, opB));
					break;
				case Token.Type.OpDivide:
					output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.ADividedByB, val, opB));
					break;
				case Token.Type.OpMod:
					output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AModB, val, opB));
					break;
				}
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}
			return val;
		}
			
		Value ParseUnaryMinus(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseNew;
			if (tokens.Peek().type != Token.Type.OpMinus) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();		// skip '-'

			AllowLineBreak(tokens); // allow a line break after a unary operator

			Value val = nextLevel(tokens);
			if (val is ValNumber) {
				// If what follows is a numeric literal, just invert it and be done!
				ValNumber valnum = (ValNumber)val;
				valnum.value = -valnum.value;
				return valnum;
			}
			// Otherwise, subtract it from 0 and return a new temporary.
			int tempNum = output.nextTempNum++;
			output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.AMinusB, TAC.Num(0), val));

			return TAC.RTemp(tempNum);
		}

		Value ParseNew(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParsePower;
			if (tokens.Peek().type != Token.Type.Keyword || tokens.Peek().text != "new") return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();		// skip 'new'

			AllowLineBreak(tokens); // allow a line break after a unary operator

			Value isa = nextLevel(tokens);
			Value result = new ValTemp(output.nextTempNum++);
			output.Add(new TAC.Line(result, TAC.Line.Op.NewA, isa));
			return result;
		}

		Value ParsePower(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel =  ParseAddressOf;
			Value val = nextLevel(tokens, asLval, statementStart);
			Token tok = tokens.Peek();
			while (tok.type == Token.Type.OpPower) {
				tokens.Dequeue();

				AllowLineBreak(tokens); // allow a line break after a binary operator

				val = FullyEvaluate(val);
				Value opB = nextLevel(tokens);
				int tempNum = output.nextTempNum++;
				output.Add(new TAC.Line(TAC.LTemp(tempNum), TAC.Line.Op.APowB, val, opB));
				val = TAC.RTemp(tempNum);

				tok = tokens.Peek();
			}
			return val;
		}


		Value ParseAddressOf(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseCallExpr;
			if (tokens.Peek().type != Token.Type.AddressOf) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();
			AllowLineBreak(tokens); // allow a line break after a unary operator
			Value val = nextLevel(tokens, true, statementStart);
			if (val is ValVar) {
				((ValVar)val).noInvoke = true;
			} else if (val is ValSeqElem) {
				((ValSeqElem)val).noInvoke = true;
			}
			return val;
		}

		Value FullyEvaluate(Value val, ValVar.LocalOnlyMode localOnlyMode = ValVar.LocalOnlyMode.Off) {
			if (val is ValVar) {
				ValVar var = (ValVar)val;
				// If var was protected with @, then return it as-is; don't attempt to call it.
				if (var.noInvoke) return val;
				if (var.identifier == output.localOnlyIdentifier) var.localOnly = localOnlyMode;
				// Don't invoke super; leave as-is so we can do special handling
				// of it at runtime.  Also, as an optimization, same for "self".
				if (var.identifier == "super" || var.identifier == "self") return val;
				// Evaluate a variable (which might be a function we need to call).
				ValTemp temp = new ValTemp(output.nextTempNum++);
				output.Add(new TAC.Line(temp, TAC.Line.Op.CallFunctionA, val, ValNumber.zero));
				return temp;
			} else if (val is ValSeqElem) {
				ValSeqElem elem = ((ValSeqElem)val);
				// If sequence element was protected with @, then return it as-is; don't attempt to call it.
				if (elem.noInvoke) return val;
				// Evaluate a sequence lookup (which might be a function we need to call).				
				ValTemp temp = new ValTemp(output.nextTempNum++);
				output.Add(new TAC.Line(temp, TAC.Line.Op.CallFunctionA, val, ValNumber.zero));
				return temp;
			}
			return val;
		}
		
		Value ParseCallExpr(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseMap;
			Value val = nextLevel(tokens, asLval, statementStart);
			while (true) {
				if (tokens.Peek().type == Token.Type.Dot) {
					tokens.Dequeue();	// discard '.'
					AllowLineBreak(tokens); // allow a line break after a binary operator
					Token nextIdent = RequireToken(tokens, Token.Type.Identifier);
					// We're chaining sequences here; look up (by invoking)
					// the previous part of the sequence, so we can build on it.
					val = FullyEvaluate(val);
					// Now build the lookup.
					val = new ValSeqElem(val, new ValString(nextIdent.text));
					if (tokens.Peek().type == Token.Type.LParen && !tokens.Peek().afterSpace) {
						// If this new element is followed by parens, we need to
						// parse it as a call right away.
						val = ParseCallArgs(val, tokens);
						//val = FullyEvaluate(val);
					}				
				} else if (tokens.Peek().type == Token.Type.LSquare && !tokens.Peek().afterSpace) {
					tokens.Dequeue();	// discard '['
					AllowLineBreak(tokens); // allow a line break after open bracket
					val = FullyEvaluate(val);
	
					if (tokens.Peek().type == Token.Type.Colon) {	// e.g., foo[:4]
						tokens.Dequeue();	// discard ':'
						AllowLineBreak(tokens); // allow a line break after colon
						Value index2 = null;
						if (tokens.Peek().type != Token.Type.RSquare) index2 = ParseExpr(tokens);
						ValTemp temp = new ValTemp(output.nextTempNum++);
						Intrinsics.CompileSlice(output.code, val, null, index2, temp.tempNum);
						val = temp;
					} else {
						Value index = ParseExpr(tokens);
						if (tokens.Peek().type == Token.Type.Colon) {	// e.g., foo[2:4] or foo[2:]
							tokens.Dequeue();	// discard ':'
							AllowLineBreak(tokens); // allow a line break after colon
							Value index2 = null;
							if (tokens.Peek().type != Token.Type.RSquare) index2 = ParseExpr(tokens);
							ValTemp temp = new ValTemp(output.nextTempNum++);
							Intrinsics.CompileSlice(output.code, val, index, index2, temp.tempNum);
							val = temp;
						} else {			// e.g., foo[3]  (not a slice at all)
							if (statementStart) {
								// At the start of a statement, we don't want to compile the
								// last sequence lookup, because we might have to convert it into
								// an assignment.  But we want to compile any previous one.
								if (val is ValSeqElem) {
									ValSeqElem vsVal = (ValSeqElem)val;
									ValTemp temp = new ValTemp(output.nextTempNum++);
									output.Add(new TAC.Line(temp, TAC.Line.Op.ElemBofA, vsVal.sequence, vsVal.index));
									val = temp;
								}
								val = new ValSeqElem(val, index);
							} else {
								// Anywhere else in an expression, we can compile the lookup right away.
								ValTemp temp = new ValTemp(output.nextTempNum++);
								output.Add(new TAC.Line(temp, TAC.Line.Op.ElemBofA, val, index));
								val = temp;
							}
						}
					}
	
					RequireToken(tokens, Token.Type.RSquare);
				} else if ((val is ValVar && !((ValVar)val).noInvoke)
					    || (val is ValSeqElem && !((ValSeqElem)val).noInvoke)) {
					// Got a variable... it might refer to a function!
					if (!asLval || (tokens.Peek().type == Token.Type.LParen && !tokens.Peek().afterSpace)) {
						// If followed by parens, definitely a function call, possibly with arguments!
						// If not, well, let's call it anyway unless we need an lvalue.
						val = ParseCallArgs(val, tokens);
					} else break;
				} else break;
			}
			
			return val;
		}

		Value ParseMap(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseList;
			if (tokens.Peek().type != Token.Type.LCurly) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();
			// NOTE: we must be sure this map gets created at runtime, not here at parse time.
			// Since it is a mutable object, we need to return a different one each time
			// this code executes (in a loop, function, etc.).  So, we use Op.CopyA below!
			ValMap map = new ValMap();
			if (tokens.Peek().type == Token.Type.RCurly) {
				tokens.Dequeue();
			} else while (true) {
				AllowLineBreak(tokens); // allow a line break after a comma or open brace

				// Allow the map to close with a } on its own line. 
				if (tokens.Peek().type == Token.Type.RCurly) {
					tokens.Dequeue();
					break;
				}

				Value key = ParseExpr(tokens);
				RequireToken(tokens, Token.Type.Colon);
				AllowLineBreak(tokens); // allow a line break after a colon
				Value value = ParseExpr(tokens);
				map.map[key ?? ValNull.instance] = value;
				
				if (RequireEitherToken(tokens, Token.Type.Comma, Token.Type.RCurly).type == Token.Type.RCurly) break;
			}
			Value result = new ValTemp(output.nextTempNum++);
			output.Add(new TAC.Line(result, TAC.Line.Op.CopyA, map));
			return result;
		}

		//		list	:= '[' expr [, expr, ...] ']'
		//				 | quantity
		Value ParseList(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseQuantity;
			if (tokens.Peek().type != Token.Type.LSquare) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();
			// NOTE: we must be sure this list gets created at runtime, not here at parse time.
			// Since it is a mutable object, we need to return a different one each time
			// this code executes (in a loop, function, etc.).  So, we use Op.CopyA below!
			ValList list = new ValList();
			if (tokens.Peek().type == Token.Type.RSquare) {
				tokens.Dequeue();
			} else while (true) {
				AllowLineBreak(tokens); // allow a line break after a comma or open bracket

				// Allow the list to close with a ] on its own line. 
				if (tokens.Peek().type == Token.Type.RSquare) {
					tokens.Dequeue();
					break;
				}

				Value elem = ParseExpr(tokens);
				list.values.Add(elem);
				if (RequireEitherToken(tokens, Token.Type.Comma, Token.Type.RSquare).type == Token.Type.RSquare) break;
			}
			Value result = new ValTemp(output.nextTempNum++);
			output.Add(new TAC.Line(result, TAC.Line.Op.CopyA, list));	// use COPY on this mutable list!
			return result;
		}

		//		quantity := '(' expr ')'
		//				  | call
		Value ParseQuantity(Lexer tokens, bool asLval=false, bool statementStart=false) {
			ExpressionParsingMethod nextLevel = ParseAtom;
			if (tokens.Peek().type != Token.Type.LParen) return nextLevel(tokens, asLval, statementStart);
			tokens.Dequeue();
			AllowLineBreak(tokens); // allow a line break after an open paren
			Value val = ParseExpr(tokens);
			RequireToken(tokens, Token.Type.RParen);
			return val;
		}

		/// <summary>
		/// Helper method that gathers arguments, emitting SetParamAasB for each one,
		/// and then emits the actual call to the given function.  It works both for
		/// a parenthesized set of arguments, and for no parens (i.e. no arguments).
		/// </summary>
		/// <returns>The call arguments.</returns>
		/// <param name="funcRef">Function to invoke.</param>
		/// <param name="tokens">Token stream.</param>
		Value ParseCallArgs(Value funcRef, Lexer tokens) {
			int argCount = 0;
			if (tokens.Peek().type == Token.Type.LParen) {
				tokens.Dequeue();		// remove '('
				if (tokens.Peek().type == Token.Type.RParen) {
					tokens.Dequeue();
				} else while (true) {
					AllowLineBreak(tokens); // allow a line break after a comma or open paren
					Value arg = ParseExpr(tokens);
					output.Add(new TAC.Line(null, TAC.Line.Op.PushParam, arg));
					argCount++;
					if (RequireEitherToken(tokens, Token.Type.Comma, Token.Type.RParen).type == Token.Type.RParen) break;
				}
			}
			ValTemp result = new ValTemp(output.nextTempNum++);
			output.Add(new TAC.Line(result, TAC.Line.Op.CallFunctionA, funcRef, TAC.Num(argCount)));
			return result;
		}
			
		Value ParseAtom(Lexer tokens, bool asLval=false, bool statementStart=false) {
			Token tok = !tokens.AtEnd ? tokens.Dequeue() : Token.EOL;
			if (tok.type == Token.Type.Number) {
                try {
                    return new ValNumber(FP.FromString(tok.text));
                } catch {
				    throw new CompilerException("invalid numeric literal: " + tok.text);
                }
			} else if (tok.type == Token.Type.String) {
				return new ValString(tok.text);
			} else if (tok.type == Token.Type.Identifier) {
				if (tok.text == "self") return ValVar.self;
				ValVar result = new ValVar(tok.text);
				if (result.identifier == output.localOnlyIdentifier) {
					result.localOnly = (output.localOnlyStrict ? ValVar.LocalOnlyMode.Strict : ValVar.LocalOnlyMode.Warn);
				}
				return result;
			} else if (tok.type == Token.Type.Keyword) {
				switch (tok.text) {
				case "null":	return null;
				case "true":	return ValNumber.one;
				case "false":	return ValNumber.zero;
				}
			}
			throw new CompilerException(string.Format("got {0} where number, string, or identifier is required", tok));
		}


		/// <summary>
		/// The given token type and text is required. So, consume the next token,
		/// and if it doesn't match, throw an error.
		/// </summary>
		/// <param name="tokens">Token queue.</param>
		/// <param name="type">Required token type.</param>
		/// <param name="text">Required token text (if applicable).</param>
		Token RequireToken(Lexer tokens, Token.Type type, string text=null) {
			Token got = (tokens.AtEnd ? Token.EOL : tokens.Dequeue());
			if (got.type != type || (text != null && got.text != text)) {
				Token expected = new Token(type, text);
				// provide a special error for the common mistake of using `=` instead of `==`
				// in an `if` condition; this will be found here:
				if (got.type == Token.Type.OpAssign && text == "then") {
					throw new CompilerException(errorContext, tokens.lineNum, 
						"found = instead of == in if condition");
				}
				throw new CompilerException(errorContext, tokens.lineNum, 
					string.Format("got {0} where {1} is required", got, expected));
			}
			return got;
		}

		Token RequireEitherToken(Lexer tokens, Token.Type type1, string text1, Token.Type type2, string text2=null) {
			Token got = (tokens.AtEnd ? Token.EOL : tokens.Dequeue());
			if ((got.type != type1 && got.type != type2)
				|| ((text1 != null && got.text != text1) && (text2 != null && got.text != text2))) {
				Token expected1 = new Token(type1, text1);
				Token expected2 = new Token(type2, text2);
				throw new CompilerException(errorContext, tokens.lineNum, 
					string.Format("got {0} where {1} or {2} is required", got, expected1, expected2));
			}
			return got;
		}

		Token RequireEitherToken(Lexer tokens, Token.Type type1, Token.Type type2, string text2=null) {
			return RequireEitherToken(tokens, type1, null, type2, text2);
		}

		static void TestValidParse(string src, bool dumpTac=false) {
			Parser parser = new Parser();
			try {
				parser.Parse(src);
			} catch (System.Exception e) {
				Console.WriteLine(e.ToString() + " while parsing:");
				Console.WriteLine(src);
			}
			if (dumpTac && parser.output != null) TAC.Dump(parser.output.code, -1);
		}

		public static void RunUnitTests() {
			TestValidParse("pi < 4");
			TestValidParse("(pi < 4)");
			TestValidParse("if true then 20 else 30");
			TestValidParse("f = function(x)\nreturn x*3\nend function\nf(14)");
			TestValidParse("foo=\"bar\"\nindexes(foo*2)\nfoo.indexes");
			TestValidParse("x=[]\nx.push(42)");
			TestValidParse("list1=[10, 20, 30, 40, 50]; range(0, list1.len)");
			TestValidParse("f = function(x); print(\"foo\"); end function; print(false and f)");
			TestValidParse("print 42");
			TestValidParse("print true");
			TestValidParse("f = function(x)\nprint x\nend function\nf 42");
			TestValidParse("myList = [1, null, 3]");
			TestValidParse("while true; if true then; break; else; print 1; end if; end while");
			TestValidParse("x = 0 or\n1");
			TestValidParse("x = [1, 2, \n 3]");
			TestValidParse("range 1,\n10, 2");
		}
	}
}

