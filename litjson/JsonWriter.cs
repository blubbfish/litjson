#region Header
/**
 * JsonWriter.cs
 *   Stream-like facility to output JSON text.
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


namespace LitJson
{
    internal enum Condition
    {
        InArray,
        InObject,
        NotAProperty,
        Property,
        Value
    }

    internal class WriterContext
    {
        public Int32 Count;
        public Boolean InArray;
        public Boolean InObject;
        public Boolean ExpectingValue;
        public Int32 Padding;
    }

  public class JsonWriter {
    #region Fields
    private static readonly NumberFormatInfo number_format;

    private WriterContext context;
    private Stack<WriterContext> ctx_stack;
    private Boolean has_reached_end;
    private Char[] hex_seq;
    private Int32 indentation;
    private Int32 indent_value;
    private StringBuilder inst_string_builder;
    private Boolean pretty_print;
    private Boolean validate;
    private Boolean lower_case_properties;
    private TextWriter writer;
    #endregion


    #region Properties
    public Int32 IndentValue {
      get { return this.indent_value; }
      set {
        this.indentation = (this.indentation / this.indent_value) * value;
        this.indent_value = value;
      }
    }

    public Boolean PrettyPrint {
      get { return this.pretty_print; }
      set { this.pretty_print = value; }
    }

    public TextWriter TextWriter {
      get { return this.writer; }
    }

    public Boolean Validate {
      get { return this.validate; }
      set { this.validate = value; }
    }

    public Boolean LowerCaseProperties {
      get { return this.lower_case_properties; }
      set { this.lower_case_properties = value; }
    }
    #endregion


    #region Constructors
    static JsonWriter() {
      number_format = NumberFormatInfo.InvariantInfo;
    }

    public JsonWriter() {
      this.inst_string_builder = new StringBuilder();
      this.writer = new StringWriter(this.inst_string_builder);

      Init();
    }

    public JsonWriter(StringBuilder sb) :
        this(new StringWriter(sb)) {
    }

    public JsonWriter(TextWriter writer) {
      this.writer = writer ?? throw new ArgumentNullException("writer");

      Init();
    }
    #endregion


    #region Private Methods
    private void DoValidation(Condition cond) {
      if (!this.context.ExpectingValue) {
        this.context.Count++;
      }

      if (!this.validate) {
        return;
      }

      if (this.has_reached_end) {
        throw new JsonException(
            "A complete JSON symbol has already been written");
      }

      switch (cond) {
        case Condition.InArray:
          if (!this.context.InArray) {
            throw new JsonException(
                "Can't close an array here");
          }

          break;

        case Condition.InObject:
          if (!this.context.InObject || this.context.ExpectingValue) {
            throw new JsonException(
                "Can't close an object here");
          }

          break;

        case Condition.NotAProperty:
          if (this.context.InObject && !this.context.ExpectingValue) {
            throw new JsonException(
                "Expected a property");
          }

          break;

        case Condition.Property:
          if (!this.context.InObject || this.context.ExpectingValue) {
            throw new JsonException(
                "Can't add a property here");
          }

          break;

        case Condition.Value:
          if (!this.context.InArray &&
              (!this.context.InObject || !this.context.ExpectingValue)) {
            throw new JsonException(
                "Can't add a value here");
          }

          break;
      }
    }

    private void Init() {
      this.has_reached_end = false;
      this.hex_seq = new Char[4];
      this.indentation = 0;
      this.indent_value = 4;
      this.pretty_print = false;
      this.validate = true;
      this.lower_case_properties = false;

      this.ctx_stack = new Stack<WriterContext>();
      this.context = new WriterContext();
      this.ctx_stack.Push(this.context);
    }

    private static void IntToHex(Int32 n, Char[] hex) {
      Int32 num;

      for (Int32 i = 0; i < 4; i++) {
        num = n % 16;

        if (num < 10) {
          hex[3 - i] = (Char)('0' + num);
        } else {
          hex[3 - i] = (Char)('A' + (num - 10));
        }

        n >>= 4;
      }
    }

    private void Indent() {
      if (this.pretty_print) {
        this.indentation += this.indent_value;
      }
    }

    private void Put(String str) {
      if (this.pretty_print && !this.context.ExpectingValue) {
        for (Int32 i = 0; i < this.indentation; i++) {
          this.writer.Write(' ');
        }
      }

      this.writer.Write(str);
    }

    private void PutNewline() {
      PutNewline(true);
    }

    private void PutNewline(Boolean add_comma) {
      if (add_comma && !this.context.ExpectingValue &&
          this.context.Count > 1) {
        this.writer.Write(',');
      }

      if (this.pretty_print && !this.context.ExpectingValue) {
        this.writer.Write(Environment.NewLine);
      }
    }

    private void PutString(String str) {
      Put(String.Empty);

      this.writer.Write('"');

      Int32 n = str.Length;
      for (Int32 i = 0; i < n; i++) {
        switch (str[i]) {
          case '\n':
            this.writer.Write("\\n");
            continue;

          case '\r':
            this.writer.Write("\\r");
            continue;

          case '\t':
            this.writer.Write("\\t");
            continue;

          case '"':
          case '\\':
            this.writer.Write('\\');
            this.writer.Write(str[i]);
            continue;

          case '\f':
            this.writer.Write("\\f");
            continue;

          case '\b':
            this.writer.Write("\\b");
            continue;
        }

        if (str[i] >= 32 && str[i] <= 126) {
          this.writer.Write(str[i]);
          continue;
        }

        // Default, turn into a \uXXXX sequence
        IntToHex(str[i], this.hex_seq);
        this.writer.Write("\\u");
        this.writer.Write(this.hex_seq);
      }

      this.writer.Write('"');
    }

    private void Unindent() {
      if (this.pretty_print) {
        this.indentation -= this.indent_value;
      }
    }
    #endregion


    public override String ToString() {
      if (this.inst_string_builder == null) {
        return String.Empty;
      }
      return this.inst_string_builder.ToString();
    }

    public void Reset() {
      this.has_reached_end = false;

      this.ctx_stack.Clear();
      this.context = new WriterContext();
      this.ctx_stack.Push(this.context);

      if (this.inst_string_builder != null) {
        this.inst_string_builder.Remove(0, this.inst_string_builder.Length);
      }
    }

    public void Write(Boolean boolean) {
      DoValidation(Condition.Value);
      PutNewline();

      Put(boolean ? "true" : "false");

      this.context.ExpectingValue = false;
    }

    public void Write(Decimal number) {
      DoValidation(Condition.Value);
      PutNewline();

      Put(Convert.ToString(number, number_format));

      this.context.ExpectingValue = false;
    }

    public void Write(Double number) {
      DoValidation(Condition.Value);
      PutNewline();

      String str = Convert.ToString(number, number_format);
      Put(str);

      if (str.IndexOf('.') == -1 && str.IndexOf('E') == -1) {
        this.writer.Write(".0");
      }

      this.context.ExpectingValue = false;
    }

    public void Write(Int32 number) {
      DoValidation(Condition.Value);
      PutNewline();

      Put(Convert.ToString(number, number_format));

      this.context.ExpectingValue = false;
    }

    public void Write(Int64 number) {
      DoValidation(Condition.Value);
      PutNewline();

      Put(Convert.ToString(number, number_format));

      this.context.ExpectingValue = false;
    }

    public void Write(String str) {
      DoValidation(Condition.Value);
      PutNewline();

      if (str == null) {
        Put("null");
      } else {
        PutString(str);
      }

      this.context.ExpectingValue = false;
    }

    // [CLSCompliant(false)]
    public void Write(UInt64 number) {
      DoValidation(Condition.Value);
      PutNewline();

      Put(Convert.ToString(number, number_format));

      this.context.ExpectingValue = false;
    }

    public void WriteArrayEnd() {
      DoValidation(Condition.InArray);
      PutNewline(false);

      this.ctx_stack.Pop();
      if (this.ctx_stack.Count == 1) {
        this.has_reached_end = true;
      } else {
        this.context = this.ctx_stack.Peek();
        this.context.ExpectingValue = false;
      }

      Unindent();
      Put("]");
    }

    public void WriteArrayStart() {
      DoValidation(Condition.NotAProperty);
      PutNewline();

      Put("[");

      this.context = new WriterContext {
        InArray = true
      };
      this.ctx_stack.Push(this.context);

      Indent();
    }

    public void WriteObjectEnd() {
      DoValidation(Condition.InObject);
      PutNewline(false);

      this.ctx_stack.Pop();
      if (this.ctx_stack.Count == 1) {
        this.has_reached_end = true;
      } else {
        this.context = this.ctx_stack.Peek();
        this.context.ExpectingValue = false;
      }

      Unindent();
      Put("}");
    }

    public void WriteObjectStart() {
      DoValidation(Condition.NotAProperty);
      PutNewline();

      Put("{");

      this.context = new WriterContext {
        InObject = true
      };
      this.ctx_stack.Push(this.context);

      Indent();
    }

    public void WritePropertyName(String property_name) {
      DoValidation(Condition.Property);
      PutNewline();
      String propertyName = (property_name == null || !this.lower_case_properties) ? property_name : property_name.ToLowerInvariant();

      PutString(propertyName);

      if (this.pretty_print) {
        if (propertyName.Length > this.context.Padding) {
          this.context.Padding = propertyName.Length;
        }

        for (Int32 i = this.context.Padding - propertyName.Length;
                     i >= 0; i--) {
          this.writer.Write(' ');
        }

        this.writer.Write(": ");
      } else {
        this.writer.Write(':');
      }

      this.context.ExpectingValue = true;
    }
  }
}
