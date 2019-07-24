#region Header
/**
 * Lexer.cs
 *   JSON lexer implementation based on a finite state machine.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 **/
#endregion


using System;
using System.IO;
using System.Text;


namespace LitJson {
  internal class FsmContext {
    public Boolean Return;
    public Int32 NextState;
    public Lexer L;
    public Int32 StateStack;
  }

  internal class Lexer {
    #region Fields
    private delegate Boolean StateHandler(FsmContext ctx);

    private static readonly Int32[] fsm_return_table;
    private static readonly StateHandler[] fsm_handler_table;
    private readonly FsmContext fsm_context;
    private Int32 input_buffer;
    private Int32 input_char;
    private readonly TextReader reader;
    private Int32 state;
    private readonly StringBuilder string_buffer;
    private Int32 unichar;
    #endregion

    #region Properties
    public Boolean AllowComments { get; set; }

    public Boolean AllowSingleQuotedStrings { get; set; }

    public Boolean EndOfInput { get; private set; }

    public Int32 Token { get; private set; }

    public String StringValue { get; private set; }
    #endregion

    #region Constructors
    static Lexer() => PopulateFsmTables(out fsm_handler_table, out fsm_return_table);

    public Lexer(TextReader reader) {
      this.AllowComments = true;
      this.AllowSingleQuotedStrings = true;

      this.input_buffer = 0;
      this.string_buffer = new StringBuilder(128);
      this.state = 1;
      this.EndOfInput = false;
      this.reader = reader;

      this.fsm_context = new FsmContext {
        L = this
      };
    }
    #endregion

    #region Static Methods
    private static Int32 HexValue(Int32 digit) {
      switch(digit) {
        case 'a':
        case 'A':
          return 10;

        case 'b':
        case 'B':
          return 11;

        case 'c':
        case 'C':
          return 12;

        case 'd':
        case 'D':
          return 13;

        case 'e':
        case 'E':
          return 14;

        case 'f':
        case 'F':
          return 15;

        default:
          return digit - '0';
      }
    }

    private static void PopulateFsmTables(out StateHandler[] fsm_handler_table, out Int32[] fsm_return_table) {
      // See section A.1. of the manual for details of the finite
      // state machine.
      fsm_handler_table = new StateHandler[28] {
        State1,
        State2,
        State3,
        State4,
        State5,
        State6,
        State7,
        State8,
        State9,
        State10,
        State11,
        State12,
        State13,
        State14,
        State15,
        State16,
        State17,
        State18,
        State19,
        State20,
        State21,
        State22,
        State23,
        State24,
        State25,
        State26,
        State27,
        State28
      };

      fsm_return_table = new Int32[28] {
        (Int32) ParserToken.Char,
        0,
        (Int32) ParserToken.Number,
        (Int32) ParserToken.Number,
        0,
        (Int32) ParserToken.Number,
        0,
        (Int32) ParserToken.Number,
        0,
        0,
        (Int32) ParserToken.True,
        0,
        0,
        0,
        (Int32) ParserToken.False,
        0,
        0,
        (Int32) ParserToken.Null,
        (Int32) ParserToken.CharSeq,
        (Int32) ParserToken.Char,
        0,
        0,
        (Int32) ParserToken.CharSeq,
        (Int32) ParserToken.Char,
        0,
        0,
        0,
        0
      };
    }

    private static Char ProcessEscChar(Int32 esc_char) {
      switch(esc_char) {
        case '"':
        case '\'':
        case '\\':
        case '/':
          return Convert.ToChar(esc_char);

        case 'n':
          return '\n';

        case 't':
          return '\t';

        case 'r':
          return '\r';

        case 'b':
          return '\b';

        case 'f':
          return '\f';

        default:
          // Unreachable
          return '?';
      }
    }

    private static Boolean State1(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        if(ctx.L.input_char == ' ' ||
            ctx.L.input_char >= '\t' && ctx.L.input_char <= '\r') {
          continue;
        }

        if(ctx.L.input_char >= '1' && ctx.L.input_char <= '9') {
          _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
          ctx.NextState = 3;
          return true;
        }

        switch(ctx.L.input_char) {
          case '"':
            ctx.NextState = 19;
            ctx.Return = true;
            return true;

          case ',':
          case ':':
          case '[':
          case ']':
          case '{':
          case '}':
            ctx.NextState = 1;
            ctx.Return = true;
            return true;

          case '-':
            _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
            ctx.NextState = 2;
            return true;

          case '0':
            _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
            ctx.NextState = 4;
            return true;

          case 'f':
            ctx.NextState = 12;
            return true;

          case 'n':
            ctx.NextState = 16;
            return true;

          case 't':
            ctx.NextState = 9;
            return true;

          case '\'':
            if(!ctx.L.AllowSingleQuotedStrings) {
              return false;
            }

            ctx.L.input_char = '"';
            ctx.NextState = 23;
            ctx.Return = true;
            return true;

          case '/':
            if(!ctx.L.AllowComments) {
              return false;
            }

            ctx.NextState = 25;
            return true;

          default:
            return false;
        }
      }

      return true;
    }

    private static Boolean State2(FsmContext ctx) {
      _ = ctx.L.GetChar();

      if(ctx.L.input_char >= '1' && ctx.L.input_char <= '9') {
        _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
        ctx.NextState = 3;
        return true;
      }

      switch(ctx.L.input_char) {
        case '0':
          _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
          ctx.NextState = 4;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State3(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        if(ctx.L.input_char >= '0' && ctx.L.input_char <= '9') {
          _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
          continue;
        }

        if(ctx.L.input_char == ' ' ||
            ctx.L.input_char >= '\t' && ctx.L.input_char <= '\r') {
          ctx.Return = true;
          ctx.NextState = 1;
          return true;
        }

        switch(ctx.L.input_char) {
          case ',':
          case ']':
          case '}':
            ctx.L.UngetChar();
            ctx.Return = true;
            ctx.NextState = 1;
            return true;

          case '.':
            _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
            ctx.NextState = 5;
            return true;

          case 'e':
          case 'E':
            _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
            ctx.NextState = 7;
            return true;

          default:
            return false;
        }
      }
      return true;
    }

    private static Boolean State4(FsmContext ctx) {
      _ = ctx.L.GetChar();

      if(ctx.L.input_char == ' ' ||
          ctx.L.input_char >= '\t' && ctx.L.input_char <= '\r') {
        ctx.Return = true;
        ctx.NextState = 1;
        return true;
      }

      switch(ctx.L.input_char) {
        case ',':
        case ']':
        case '}':
          ctx.L.UngetChar();
          ctx.Return = true;
          ctx.NextState = 1;
          return true;

        case '.':
          _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
          ctx.NextState = 5;
          return true;

        case 'e':
        case 'E':
          _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
          ctx.NextState = 7;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State5(FsmContext ctx) {
      _ = ctx.L.GetChar();

      if(ctx.L.input_char >= '0' && ctx.L.input_char <= '9') {
        _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
        ctx.NextState = 6;
        return true;
      }

      return false;
    }

    private static Boolean State6(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        if(ctx.L.input_char >= '0' && ctx.L.input_char <= '9') {
          _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
          continue;
        }

        if(ctx.L.input_char == ' ' ||
            ctx.L.input_char >= '\t' && ctx.L.input_char <= '\r') {
          ctx.Return = true;
          ctx.NextState = 1;
          return true;
        }

        switch(ctx.L.input_char) {
          case ',':
          case ']':
          case '}':
            ctx.L.UngetChar();
            ctx.Return = true;
            ctx.NextState = 1;
            return true;

          case 'e':
          case 'E':
            _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
            ctx.NextState = 7;
            return true;

          default:
            return false;
        }
      }

      return true;
    }

    private static Boolean State7(FsmContext ctx) {
      _ = ctx.L.GetChar();

      if(ctx.L.input_char >= '0' && ctx.L.input_char <= '9') {
        _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
        ctx.NextState = 8;
        return true;
      }

      switch(ctx.L.input_char) {
        case '+':
        case '-':
          _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
          ctx.NextState = 8;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State8(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        if(ctx.L.input_char >= '0' && ctx.L.input_char <= '9') {
          _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
          continue;
        }

        if(ctx.L.input_char == ' ' ||
            ctx.L.input_char >= '\t' && ctx.L.input_char <= '\r') {
          ctx.Return = true;
          ctx.NextState = 1;
          return true;
        }

        switch(ctx.L.input_char) {
          case ',':
          case ']':
          case '}':
            ctx.L.UngetChar();
            ctx.Return = true;
            ctx.NextState = 1;
            return true;

          default:
            return false;
        }
      }

      return true;
    }

    private static Boolean State9(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'r':
          ctx.NextState = 10;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State10(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'u':
          ctx.NextState = 11;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State11(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'e':
          ctx.Return = true;
          ctx.NextState = 1;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State12(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'a':
          ctx.NextState = 13;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State13(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'l':
          ctx.NextState = 14;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State14(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 's':
          ctx.NextState = 15;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State15(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'e':
          ctx.Return = true;
          ctx.NextState = 1;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State16(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'u':
          ctx.NextState = 17;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State17(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'l':
          ctx.NextState = 18;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State18(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'l':
          ctx.Return = true;
          ctx.NextState = 1;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State19(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        switch(ctx.L.input_char) {
          case '"':
            ctx.L.UngetChar();
            ctx.Return = true;
            ctx.NextState = 20;
            return true;

          case '\\':
            ctx.StateStack = 19;
            ctx.NextState = 21;
            return true;

          default:
            _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
            continue;
        }
      }

      return true;
    }

    private static Boolean State20(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case '"':
          ctx.Return = true;
          ctx.NextState = 1;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State21(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case 'u':
          ctx.NextState = 22;
          return true;

        case '"':
        case '\'':
        case '/':
        case '\\':
        case 'b':
        case 'f':
        case 'n':
        case 'r':
        case 't':
          _ = ctx.L.string_buffer.Append(
              ProcessEscChar(ctx.L.input_char));
          ctx.NextState = ctx.StateStack;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State22(FsmContext ctx) {
      Int32 counter = 0;
      Int32 mult = 4096;

      ctx.L.unichar = 0;

      while(ctx.L.GetChar()) {

        if(ctx.L.input_char >= '0' && ctx.L.input_char <= '9' ||
            ctx.L.input_char >= 'A' && ctx.L.input_char <= 'F' ||
            ctx.L.input_char >= 'a' && ctx.L.input_char <= 'f') {

          ctx.L.unichar += HexValue(ctx.L.input_char) * mult;

          counter++;
          mult /= 16;

          if(counter == 4) {
            _ = ctx.L.string_buffer.Append(
                Convert.ToChar(ctx.L.unichar));
            ctx.NextState = ctx.StateStack;
            return true;
          }

          continue;
        }

        return false;
      }

      return true;
    }

    private static Boolean State23(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        switch(ctx.L.input_char) {
          case '\'':
            ctx.L.UngetChar();
            ctx.Return = true;
            ctx.NextState = 24;
            return true;

          case '\\':
            ctx.StateStack = 23;
            ctx.NextState = 21;
            return true;

          default:
            _ = ctx.L.string_buffer.Append((Char)ctx.L.input_char);
            continue;
        }
      }

      return true;
    }

    private static Boolean State24(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case '\'':
          ctx.L.input_char = '"';
          ctx.Return = true;
          ctx.NextState = 1;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State25(FsmContext ctx) {
      _ = ctx.L.GetChar();

      switch(ctx.L.input_char) {
        case '*':
          ctx.NextState = 27;
          return true;

        case '/':
          ctx.NextState = 26;
          return true;

        default:
          return false;
      }
    }

    private static Boolean State26(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        if(ctx.L.input_char == '\n') {
          ctx.NextState = 1;
          return true;
        }
      }

      return true;
    }

    private static Boolean State27(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        if(ctx.L.input_char == '*') {
          ctx.NextState = 28;
          return true;
        }
      }

      return true;
    }

    private static Boolean State28(FsmContext ctx) {
      while(ctx.L.GetChar()) {
        if(ctx.L.input_char == '*') {
          continue;
        }

        if(ctx.L.input_char == '/') {
          ctx.NextState = 1;
          return true;
        }

        ctx.NextState = 27;
        return true;
      }

      return true;
    }
    #endregion

    private Boolean GetChar() {
      if((this.input_char = this.NextChar()) != -1) {
        return true;
      }

      this.EndOfInput = true;
      return false;
    }

    private Int32 NextChar() {
      if(this.input_buffer != 0) {
        Int32 tmp = this.input_buffer;
        this.input_buffer = 0;

        return tmp;
      }

      return this.reader.Read();
    }

    public Boolean NextToken() {
      StateHandler handler;
      this.fsm_context.Return = false;

      while(true) {
        handler = fsm_handler_table[this.state - 1];

        if(!handler(this.fsm_context)) {
          throw new JsonException(this.input_char);
        }

        if(this.EndOfInput) {
          return false;
        }

        if(this.fsm_context.Return) {
          this.StringValue = this.string_buffer.ToString();
          _ = this.string_buffer.Remove(0, this.string_buffer.Length);
          this.Token = fsm_return_table[this.state - 1];

          if(this.Token == (Int32)ParserToken.Char) {
            this.Token = this.input_char;
          }

          this.state = this.fsm_context.NextState;

          return true;
        }

        this.state = this.fsm_context.NextState;
      }
    }

    private void UngetChar() => this.input_buffer = this.input_char;
  }
}
