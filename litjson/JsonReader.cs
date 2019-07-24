#region Header
/**
 * JsonReader.cs
 *   Stream-like access to JSON text.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 **/
#endregion


using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;


namespace LitJson {
  public enum JsonToken {
    None,
    ObjectStart,
    PropertyName,
    ObjectEnd,
    ArrayStart,
    ArrayEnd,
    Int,
    Long,
    Double,
    String,
    Boolean,
    Null
  }


  public class JsonReader {
    #region Fields
    private static readonly IDictionary<Int32, IDictionary<Int32, Int32[]>> parse_table;

    private readonly Stack<Int32> automaton_stack;
    private Int32 current_input;
    private Int32 current_symbol;
    private readonly Lexer lexer;
    private Boolean parser_in_string;
    private Boolean parser_return;
    private Boolean read_started;
    private TextReader reader;
    private readonly Boolean reader_is_owned;
    #endregion

    #region Public Properties
    public Boolean AllowComments {
      get => this.lexer.AllowComments;
      set => this.lexer.AllowComments = value;
    }

    public Boolean AllowSingleQuotedStrings {
      get => this.lexer.AllowSingleQuotedStrings;
      set => this.lexer.AllowSingleQuotedStrings = value;
    }

    public Boolean SkipNonMembers { get; set; }

    public Boolean EndOfInput { get; private set; }

    public Boolean EndOfJson { get; private set; }

    public JsonToken Token { get; private set; }

    public Object Value { get; private set; }
    #endregion

    #region Constructors
    static JsonReader() => parse_table = PopulateParseTable();

    public JsonReader(String json_text) : this(new StringReader(json_text), true) { }

    public JsonReader(TextReader reader) : this(reader, false) { }

    private JsonReader(TextReader reader, Boolean owned) {
      if(reader == null) {
        throw new ArgumentNullException("reader");
      }

      this.parser_in_string = false;
      this.parser_return = false;

      this.read_started = false;
      this.automaton_stack = new Stack<Int32>();
      this.automaton_stack.Push((Int32)ParserToken.End);
      this.automaton_stack.Push((Int32)ParserToken.Text);

      this.lexer = new Lexer(reader);

      this.EndOfInput = false;
      this.EndOfJson = false;

      this.SkipNonMembers = true;

      this.reader = reader;
      this.reader_is_owned = owned;
    }
    #endregion

    #region Static Methods
    private static IDictionary<Int32, IDictionary<Int32, Int32[]>> PopulateParseTable() =>
      // See section A.2. of the manual for details
      new Dictionary<Int32, IDictionary<Int32, Int32[]>> {
        {
          (Int32)ParserToken.Array,
          new Dictionary<Int32, Int32[]> {
            { '[', new Int32[] { '[', (Int32)ParserToken.ArrayPrime } }
          }
        },
        {
          (Int32)ParserToken.ArrayPrime,
          new Dictionary<Int32, Int32[]> {
            { '"', new Int32[] { (Int32)ParserToken.Value, (Int32)ParserToken.ValueRest, ']' } },
            { '[', new Int32[] { (Int32)ParserToken.Value, (Int32)ParserToken.ValueRest, ']' } },
            { ']', new Int32[] { ']' } },
            { '{', new Int32[] { (Int32)ParserToken.Value, (Int32)ParserToken.ValueRest, ']' } },
            { (Int32)ParserToken.Number, new Int32[] { (Int32)ParserToken.Value, (Int32)ParserToken.ValueRest, ']' } },
            { (Int32)ParserToken.True, new Int32[] { (Int32)ParserToken.Value, (Int32)ParserToken.ValueRest, ']' } },
            { (Int32)ParserToken.False, new Int32[] { (Int32)ParserToken.Value, (Int32)ParserToken.ValueRest, ']' } },
            { (Int32)ParserToken.Null, new Int32[] { (Int32)ParserToken.Value, (Int32)ParserToken.ValueRest, ']' } }
          }
        },
        {
          (Int32)ParserToken.Object,
          new Dictionary<Int32, Int32[]> {
            { '{', new Int32[] { '{', (Int32)ParserToken.ObjectPrime } }
          }
        },
        {
          (Int32)ParserToken.ObjectPrime,
          new Dictionary<Int32, Int32[]> {
            { '"', new Int32[] { (Int32)ParserToken.Pair, (Int32)ParserToken.PairRest, '}' } },
            { '}', new Int32[] { '}' } }
          }
        },
        {
          (Int32)ParserToken.Pair,
          new Dictionary<Int32, Int32[]> {
            { '"', new Int32[] { (Int32)ParserToken.String, ':', (Int32)ParserToken.Value } }
          }
        },
        {
          (Int32)ParserToken.PairRest,
          new Dictionary<Int32, Int32[]> {
            { ',', new Int32[] { ',', (Int32)ParserToken.Pair, (Int32)ParserToken.PairRest } },
            { '}', new Int32[] { (Int32)ParserToken.Epsilon } }
          }
        },
        {
          (Int32)ParserToken.String,
          new Dictionary<Int32, Int32[]> {
            { '"', new Int32[] { '"', (Int32)ParserToken.CharSeq, '"' } }
          }
        },
        {
          (Int32)ParserToken.Text,
          new Dictionary<Int32, Int32[]> {
            { '[', new Int32[] { (Int32)ParserToken.Array } },
            { '{', new Int32[] { (Int32)ParserToken.Object } }
          }
        },
        {
          (Int32)ParserToken.Value,
          new Dictionary<Int32, Int32[]> {
            { '"', new Int32[] { (Int32)ParserToken.String } },
            { '[', new Int32[] { (Int32)ParserToken.Array } },
            { '{', new Int32[] { (Int32)ParserToken.Object } },
            { (Int32)ParserToken.Number, new Int32[] { (Int32)ParserToken.Number } },
            { (Int32)ParserToken.True, new Int32[] { (Int32)ParserToken.True } },
            { (Int32)ParserToken.False, new Int32[] { (Int32)ParserToken.False } },
            { (Int32)ParserToken.Null, new Int32[] { (Int32)ParserToken.Null } }
          }
        },
        {
          (Int32)ParserToken.ValueRest,
          new Dictionary<Int32, Int32[]> {
            { ',', new Int32[] { ',', (Int32)ParserToken.Value, (Int32)ParserToken.ValueRest } },
            { ']', new Int32[] { (Int32)ParserToken.Epsilon } }
          }
        }
      };
    #endregion

    #region Private Methods
    private void ProcessNumber(String number) {
      if(number.IndexOf('.') != -1 || number.IndexOf('e') != -1 || number.IndexOf('E') != -1) {
        if(Double.TryParse(number, NumberStyles.Any, CultureInfo.InvariantCulture, out Double n_double)) {
          this.Token = JsonToken.Double;
          this.Value = n_double;
          return;
        }
      }

      if(Int32.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int32 n_int32)) {
        this.Token = JsonToken.Int;
        this.Value = n_int32;
        return;
      }

      if(Int64.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out Int64 n_int64)) {
        this.Token = JsonToken.Long;
        this.Value = n_int64;
        return;
      }

      if(UInt64.TryParse(number, NumberStyles.Integer, CultureInfo.InvariantCulture, out UInt64 n_uint64)) {
        this.Token = JsonToken.Long;
        this.Value = n_uint64;
        return;
      }

      // Shouldn't happen, but just in case, return something
      this.Token = JsonToken.Int;
      this.Value = 0;
    }

    private void ProcessSymbol() {
      if(this.current_symbol == '[') {
        this.Token = JsonToken.ArrayStart;
        this.parser_return = true;
      } else if(this.current_symbol == ']') {
        this.Token = JsonToken.ArrayEnd;
        this.parser_return = true;
      } else if(this.current_symbol == '{') {
        this.Token = JsonToken.ObjectStart;
        this.parser_return = true;
      } else if(this.current_symbol == '}') {
        this.Token = JsonToken.ObjectEnd;
        this.parser_return = true;
      } else if(this.current_symbol == '"') {
        if(this.parser_in_string) {
          this.parser_in_string = false;
          this.parser_return = true;
        } else {
          if(this.Token == JsonToken.None) {
            this.Token = JsonToken.String;
          }
          this.parser_in_string = true;
        }
      } else if(this.current_symbol == (Int32)ParserToken.CharSeq) {
        this.Value = this.lexer.StringValue;
      } else if(this.current_symbol == (Int32)ParserToken.False) {
        this.Token = JsonToken.Boolean;
        this.Value = false;
        this.parser_return = true;
      } else if(this.current_symbol == (Int32)ParserToken.Null) {
        this.Token = JsonToken.Null;
        this.parser_return = true;
      } else if(this.current_symbol == (Int32)ParserToken.Number) {
        this.ProcessNumber(this.lexer.StringValue);
        this.parser_return = true;
      } else if(this.current_symbol == (Int32)ParserToken.Pair) {
        this.Token = JsonToken.PropertyName;
      } else if(this.current_symbol == (Int32)ParserToken.True) {
        this.Token = JsonToken.Boolean;
        this.Value = true;
        this.parser_return = true;
      }
    }

    private Boolean ReadToken() {
      if(this.EndOfInput) {
        return false;
      }
      _ = this.lexer.NextToken();
      if(this.lexer.EndOfInput) {
        this.Close();
        return false;
      }
      this.current_input = this.lexer.Token;
      return true;
    }
    #endregion


    public void Close() {
      if(this.EndOfInput) {
        return;
      }
      this.EndOfInput = true;
      this.EndOfJson = true;
      if(this.reader_is_owned) {
        using(this.reader) {
        }
      }
      this.reader = null;
    }

    public Boolean Read() {
      if(this.EndOfInput) {
        return false;
      }
      if(this.EndOfJson) {
        this.EndOfJson = false;
        this.automaton_stack.Clear();
        this.automaton_stack.Push((Int32)ParserToken.End);
        this.automaton_stack.Push((Int32)ParserToken.Text);
      }
      this.parser_in_string = false;
      this.parser_return = false;
      this.Token = JsonToken.None;
      this.Value = null;
      if(!this.read_started) {
        this.read_started = true;
        if(!this.ReadToken()) {
          return false;
        }
      }


      Int32[] entry_symbols;

      while(true) {
        if(this.parser_return) {
          if(this.automaton_stack.Peek() == (Int32)ParserToken.End) {
            this.EndOfJson = true;
          }

          return true;
        }

        this.current_symbol = this.automaton_stack.Pop();

        this.ProcessSymbol();

        if(this.current_symbol == this.current_input) {
          if(!this.ReadToken()) {
            if(this.automaton_stack.Peek() != (Int32)ParserToken.End) {
              throw new JsonException(
                  "Input doesn't evaluate to proper JSON text");
            }

            if(this.parser_return) {
              return true;
            }

            return false;
          }

          continue;
        }

        try {

          entry_symbols =
              parse_table[this.current_symbol][this.current_input];

        } catch(KeyNotFoundException e) {
          throw new JsonException((ParserToken)this.current_input, e);
        }

        if(entry_symbols[0] == (Int32)ParserToken.Epsilon) {
          continue;
        }

        for(Int32 i = entry_symbols.Length - 1; i >= 0; i--) {
          this.automaton_stack.Push(entry_symbols[i]);
        }
      }
    }

  }
}
