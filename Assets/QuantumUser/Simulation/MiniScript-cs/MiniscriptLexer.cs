/*	MiniscriptLexer.cs

This file is used internally during parsing of the code, breaking source
code text into a series of tokens.

Unless you’re writing a fancy MiniScript code editor, you probably don’t 
need to worry about this stuff. 

*/
using System;
using System.Collections.Generic;

namespace Miniscript {
	public class Token {
		public enum Type {
			Unknown,
			Keyword,
			Number,
			String,
			Identifier,
			OpAssign,
			OpPlus,
			OpMinus,
			OpTimes,
			OpDivide,
			OpMod,
			OpPower,
			OpEqual,
			OpNotEqual,
			OpGreater,
			OpGreatEqual,
			OpLesser,
			OpLessEqual,
			OpAssignPlus,
			OpAssignMinus,
			OpAssignTimes,
			OpAssignDivide,
			OpAssignMod,
			OpAssignPower,
			LParen,
			RParen,
			LSquare,
			RSquare,
			LCurly,
			RCurly,
			AddressOf,
			Comma,
			Dot,
			Colon,
			Comment,
			EOL
		}
		public Type type;
		public string text;	// may be null for things like operators, whose text is fixed
		public bool afterSpace;
		
		public Token(Type type=Type.Unknown, string text=null) {
			this.type = type;
			this.text = text;
		}

		public override string ToString() {
			if (text == null) return type.ToString();
			return string.Format("{0}({1})", type, text);
		}

		public static Token EOL = new Token() { type=Type.EOL };
	}
				
	public class Lexer {
		public int lineNum = 1;	// start at 1, so we report 1-based line numbers
		public int position;
		
		string input;
		int inputLength;

		Queue<Token> pending;

		public bool AtEnd {
			get { return position >= inputLength && pending.Count == 0; }
		}

		public Lexer(string input) {
			this.input = input;
			inputLength = input.Length;
			position = 0;
			pending = new Queue<Token>();
		}

		public Token Peek() {
			if (pending.Count == 0) {
				if (AtEnd) return Token.EOL;
				pending.Enqueue(Dequeue());
			}
			return pending.Peek();
		}

		public Token Dequeue() {
			if (pending.Count > 0) return pending.Dequeue();

			int oldPos = position;
			SkipWhitespaceAndComment();

			if (AtEnd) return Token.EOL;

			Token result = new Token();
			result.afterSpace = (position > oldPos);
			int startPos = position;
			char c = input[position++];

			// Handle two-character operators first.
			if (!AtEnd) {
				char c2 = input[position];
				if (c2 == '=') {
					if (c == '=') result.type = Token.Type.OpEqual;
					else if (c == '+') result.type = Token.Type.OpAssignPlus;
					else if (c == '-') result.type = Token.Type.OpAssignMinus;
					else if (c == '*') result.type = Token.Type.OpAssignTimes;
					else if (c == '/') result.type = Token.Type.OpAssignDivide;
					else if (c == '%') result.type = Token.Type.OpAssignMod;
					else if (c == '^') result.type = Token.Type.OpAssignPower;					
				}
				if (c == '!' && c2 == '=') result.type = Token.Type.OpNotEqual;
				if (c == '>' && c2 == '=') result.type = Token.Type.OpGreatEqual;
				if (c == '<' && c2 == '=') result.type = Token.Type.OpLessEqual;

				if (result.type != Token.Type.Unknown) {
					position++;
					return result;
				}
			}

			// Handle one-char operators next.
			if (c == '+') result.type = Token.Type.OpPlus;
			else if (c == '-') result.type = Token.Type.OpMinus;
			else if (c == '*') result.type = Token.Type.OpTimes;
			else if (c == '/') result.type = Token.Type.OpDivide;
			else if (c == '%') result.type = Token.Type.OpMod;
			else if (c == '^') result.type = Token.Type.OpPower;
			else if (c == '(') result.type = Token.Type.LParen;
			else if (c == ')') result.type = Token.Type.RParen;
			else if (c == '[') result.type = Token.Type.LSquare;
			else if (c == ']') result.type = Token.Type.RSquare;
			else if (c == '{') result.type = Token.Type.LCurly;
			else if (c == '}') result.type = Token.Type.RCurly;
			else if (c == ',') result.type = Token.Type.Comma;
			else if (c == ':') result.type = Token.Type.Colon;
			else if (c == '=') result.type = Token.Type.OpAssign;
			else if (c == '<') result.type = Token.Type.OpLesser;
			else if (c == '>') result.type = Token.Type.OpGreater;
			else if (c == '@') result.type = Token.Type.AddressOf;
			else if (c == ';' || c == '\n') {
				result.type = Token.Type.EOL;
				result.text = c == ';' ? ";" : "\n";
				if (c != ';') lineNum++;
			}
			if (c == '\r') {
				// Careful; DOS may use \r\n, so we need to check for that too.
				result.type = Token.Type.EOL;
				if (position < inputLength && input[position] == '\n') {
					position++;
					result.text = "\r\n";
				} else {
					result.text = "\r";
				}
				lineNum++;
			}
			if (result.type != Token.Type.Unknown) return result;

			// Then, handle more extended tokens.

			if (c == '.') {
				// A token that starts with a dot is just Type.Dot, UNLESS
				// it is followed by a number, in which case it's a decimal number.
				if (position >= inputLength || !IsNumeric(input[position])) {
					result.type = Token.Type.Dot;
					return result;
				}
			}

			if (c == '.' || IsNumeric(c)) {
				result.type = Token.Type.Number;
				while (position < inputLength) {
					char lastc = c;
					c = input[position];
					if (IsNumeric(c) || c == '.' || c == 'E' || c == 'e' ||
					    ((c == '-' || c == '+') && (lastc == 'E' || lastc == 'e'))) {
						position++;
					} else break;
				}
			} else if (IsIdentifier(c)) {
				while (position < inputLength) {
					if (IsIdentifier(input[position])) position++;
					else break;
				}
				result.text = input.Substring(startPos, position - startPos);
				result.type = (Keywords.IsKeyword(result.text) ? Token.Type.Keyword : Token.Type.Identifier);
				if (result.text == "end") {
					// As a special case: when we see "end", grab the next keyword (after whitespace)
					// too, and conjoin it, so our token is "end if", "end function", etc.
					Token nextWord = Dequeue();
					if (nextWord != null && nextWord.type == Token.Type.Keyword) {
						result.text = result.text + " " + nextWord.text;
					} else {
						// Oops, didn't find another keyword.  User error.
						throw new LexerException("'end' without following keyword ('if', 'function', etc.)");
					}
				} else if (result.text == "else") {
					// And similarly, conjoin an "if" after "else" (to make "else if").
					// (Note we can't use Peek or Dequeue/Enqueue for these, because we are probably
					// inside a Peek call already, and that would end up swapping the order of these tokens.)
					var p = position;
					while (p < inputLength && (input[p]==' ' || input[p]=='\t')) p++;
					if (p+1 < inputLength && input.Substring(p,2) == "if" &&
							(p+2 >= inputLength || !IsIdentifier(input[p+2]))) {
						result.text = "else if";
						position = p + 2;
					}
				}
				return result;
			} else if (c == '"') {
				// Lex a string... to the closing ", but skipping (and singling) a doubled double quote ("")
				result.type = Token.Type.String;
				bool haveDoubledQuotes = false;
				startPos = position;
				bool gotEndQuote = false;
				while (position < inputLength) {
					c = input[position++];
					if (c == '"') {
						if (position < inputLength && input[position] == '"') {
							// This is just a doubled quote.
							haveDoubledQuotes = true;
							position++;
						} else {
							// This is the closing quote, marking the end of the string.
							gotEndQuote = true;
							break;
						}
					} else if (c == '\n' || c == '\r') {
						// Break at end of line (string literals should not contain a line break).
						break;
					}
				}
				if (!gotEndQuote) throw new LexerException("missing closing quote (\")");
				result.text = input.Substring(startPos, position-startPos-1);
				if (haveDoubledQuotes) result.text = result.text.Replace("\"\"", "\"");
				return result;

			} else {
				result.type = Token.Type.Unknown;
			}

			result.text = input.Substring(startPos, position - startPos);
			return result;
		}

		void SkipWhitespaceAndComment() {
			while (!AtEnd && IsWhitespace(input[position])) {
				position++;
			}

			if (position < input.Length - 1 && input[position] == '/' && input[position + 1] == '/') {
				// Comment.  Skip to end of line.
				position += 2;
				while (!AtEnd && input[position] != '\n') position++;
			}
		}
		
		public static bool IsNumeric(char c) {
			return c >= '0' && c <= '9';
		}

		public static bool IsIdentifier(char c) {
			return c == '_'
				|| (c >= 'a' && c <= 'z')
				|| (c >= 'A' && c <= 'Z')
				|| (c >= '0' && c <= '9')
				|| c > '\u009F';
		}

		public static bool IsWhitespace(char c) {
			return c == ' ' || c == '\t';
		}
		
		public bool IsAtWhitespace() {
			// Caution: ignores queue, and uses only current position
			return AtEnd || IsWhitespace(input[position]);
		}

		public static bool IsInStringLiteral(int charPos, string source, int startPos=0) {
			bool inString = false;
			for (int i=startPos; i<charPos; i++) {
				if (source[i] == '"') inString = !inString;
			}
			return inString;
		}

		public static int CommentStartPos(string source, int startPos) {
			// Find the first occurrence of "//" in this line that
			// is not within a string literal.
			int commentStart = startPos-2;
			while (true) {
				commentStart = source.IndexOf("//", commentStart + 2);
				if (commentStart < 0) break;	// no comment found
				if (!IsInStringLiteral(commentStart, source, startPos)) break;	// valid comment
			}
			return commentStart;
		}
		
		public static string TrimComment(string source) {
			int startPos = source.LastIndexOf('\n') + 1;
			int commentStart = CommentStartPos(source, startPos);
			if (commentStart >= 0) return source.Substring(startPos, commentStart - startPos);
			return source;
		}

		// Find the last token in the given source, ignoring any whitespace
		// or comment at the end of that line.
		public static Token LastToken(string source) {
			// Start by finding the start and logical  end of the last line.
			int startPos = source.LastIndexOf('\n') + 1;
			int commentStart = CommentStartPos(source, startPos);
			
			// Walk back from end of string or start of comment, skipping whitespace.
			int endPos = (commentStart >= 0 ? commentStart-1 : source.Length - 1);
			while (endPos >= 0 && IsWhitespace(source[endPos])) endPos--;
			if (endPos < 0) return Token.EOL;
			
			// Find the start of that last token.
			// There are several cases to consider here.
			int tokStart = endPos;
			char c = source[endPos];
			if (IsIdentifier(c)) {
				while (tokStart > startPos && IsIdentifier(source[tokStart-1])) tokStart--;
			} else if (c == '"') {
				bool inQuote = true;
				while (tokStart > startPos) {
					tokStart--;
					if (source[tokStart] == '"') {
						inQuote = !inQuote;
						if (!inQuote && tokStart > startPos && source[tokStart-1] != '"') break;
					}
				}
			} else if (c == '=' && tokStart > startPos) {
				char c2 = source[tokStart-1];
				if (c2 == '>' || c2 == '<' || c2 == '=' || c2 == '!') tokStart--;
			}
			
			// Now use the standard lexer to grab just that bit.
			Lexer lex = new Lexer(source);
			lex.position = tokStart;
			return lex.Dequeue();
		}

		public static void Check(Token tok, Token.Type type, string text=null, int lineNum=0) {
			UnitTest.ErrorIfNull(tok);
			if (tok == null) return;
			UnitTest.ErrorIf(tok.type != type, "Token type: expected "
						+ type + ", but got " + tok.type);

			UnitTest.ErrorIf(text != null && tok.text != text,
						"Token text: expected " + text + ", but got " + tok.text);

		}

		public static void CheckLineNum(int actual, int expected) {
			UnitTest.ErrorIf(actual != expected, "Lexer line number: expected "
				+ expected + ", but got " + actual);
		}

		public static void RunUnitTests() {
			Lexer lex = new Lexer("42  * 3.14158");
			Check(lex.Dequeue(), Token.Type.Number, "42");
			CheckLineNum(lex.lineNum, 1);
			Check(lex.Dequeue(), Token.Type.OpTimes);
			Check(lex.Dequeue(), Token.Type.Number, "3.14158");
			UnitTest.ErrorIf(!lex.AtEnd, "AtEnd not set when it should be");
			CheckLineNum(lex.lineNum, 1);

			lex = new Lexer("6*(.1-foo) end if // and a comment!");
			Check(lex.Dequeue(), Token.Type.Number, "6");
			CheckLineNum(lex.lineNum, 1);
			Check(lex.Dequeue(), Token.Type.OpTimes);
			Check(lex.Dequeue(), Token.Type.LParen);
			Check(lex.Dequeue(), Token.Type.Number, ".1");
			Check(lex.Dequeue(), Token.Type.OpMinus);
			Check(lex.Peek(), Token.Type.Identifier, "foo");
			Check(lex.Peek(), Token.Type.Identifier, "foo");
			Check(lex.Dequeue(), Token.Type.Identifier, "foo");
			Check(lex.Dequeue(), Token.Type.RParen);
			Check(lex.Dequeue(), Token.Type.Keyword, "end if");
			Check(lex.Dequeue(), Token.Type.EOL);
			UnitTest.ErrorIf(!lex.AtEnd, "AtEnd not set when it should be");
			CheckLineNum(lex.lineNum, 1);

			lex = new Lexer("\"foo\" \"isn't \"\"real\"\"\" \"now \"\"\"\" double!\"");
			Check(lex.Dequeue(), Token.Type.String, "foo");
			Check(lex.Dequeue(), Token.Type.String, "isn't \"real\"");
			Check(lex.Dequeue(), Token.Type.String, "now \"\" double!");
			UnitTest.ErrorIf(!lex.AtEnd, "AtEnd not set when it should be");

			lex = new Lexer("foo\nbar\rbaz\r\nbamf");
			Check(lex.Dequeue(), Token.Type.Identifier, "foo");
			CheckLineNum(lex.lineNum, 1);
			Check(lex.Dequeue(), Token.Type.EOL);
			Check(lex.Dequeue(), Token.Type.Identifier, "bar");
			CheckLineNum(lex.lineNum, 2);
			Check(lex.Dequeue(), Token.Type.EOL);
			Check(lex.Dequeue(), Token.Type.Identifier, "baz");
			CheckLineNum(lex.lineNum, 3);
			Check(lex.Dequeue(), Token.Type.EOL);
			Check(lex.Dequeue(), Token.Type.Identifier, "bamf");
			CheckLineNum(lex.lineNum, 4);
			Check(lex.Dequeue(), Token.Type.EOL);
			UnitTest.ErrorIf(!lex.AtEnd, "AtEnd not set when it should be");
			
			lex = new Lexer("x += 42");
			Check(lex.Dequeue(), Token.Type.Identifier, "x");
			CheckLineNum(lex.lineNum, 1);
			Check(lex.Dequeue(), Token.Type.OpAssignPlus);
			Check(lex.Dequeue(), Token.Type.Number, "42");
			UnitTest.ErrorIf(!lex.AtEnd, "AtEnd not set when it should be");
			
			Check(LastToken("x=42 // foo"), Token.Type.Number, "42");
			Check(LastToken("x = [1, 2, // foo"), Token.Type.Comma);
			Check(LastToken("x = [1, 2 // foo"), Token.Type.Number, "2");
			Check(LastToken("x = [1, 2 // foo // and \"more\" foo"), Token.Type.Number, "2");
			Check(LastToken("x = [\"foo\", \"//bar\"]"), Token.Type.RSquare);
			Check(LastToken("print 1 // line 1\nprint 2"), Token.Type.Number, "2");			
			Check(LastToken("print \"Hi\"\"Quote\" // foo bar"), Token.Type.String, "Hi\"Quote");			
		}
	}
}

