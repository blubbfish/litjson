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
  internal enum Condition {
    InArray,
    InObject,
    NotAProperty,
    Property,
    Value
  }

  internal class WriterContext {
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
    private readonly StringBuilder inst_string_builder;
    #endregion

    #region Properties
    public Int32 IndentValue {
      get => this.indent_value;
      set {
        this.indentation = (this.indentation / this.indent_value) * value;
        this.indent_value = value;
      }
    }

    public Boolean PrettyPrint { get; set; }

    public TextWriter TextWriter { get; }

    public Boolean Validate { get; set; }

    public Boolean LowerCaseProperties { get; set; }
    #endregion

    #region Constructors
    static JsonWriter() => number_format = NumberFormatInfo.InvariantInfo;

    public JsonWriter() {
      this.inst_string_builder = new StringBuilder();
      this.TextWriter = new StringWriter(this.inst_string_builder);
      this.Init();
    }

    public JsonWriter(StringBuilder sb) : this(new StringWriter(sb)) { }

    public JsonWriter(TextWriter writer) {
      this.TextWriter = writer ?? throw new ArgumentNullException("writer");
      this.Init();
    }
    #endregion

    #region Private Methods
    private void DoValidation(Condition cond) {
      if (!this.context.ExpectingValue) {
        this.context.Count++;
      }

      if (!this.Validate) {
        return;
      }

      if (this.has_reached_end) {
        throw new JsonException("A complete JSON symbol has already been written");
      }

      switch (cond) {
        case Condition.InArray:
          if (!this.context.InArray) {
            throw new JsonException("Can't close an array here");
          }

          break;

        case Condition.InObject:
          if (!this.context.InObject || this.context.ExpectingValue) {
            throw new JsonException("Can't close an object here");
          }

          break;

        case Condition.NotAProperty:
          if (this.context.InObject && !this.context.ExpectingValue) {
            throw new JsonException("Expected a property");
          }

          break;

        case Condition.Property:
          if (!this.context.InObject || this.context.ExpectingValue) {
            throw new JsonException("Can't add a property here");
          }

          break;

        case Condition.Value:
          if (!this.context.InArray && (!this.context.InObject || !this.context.ExpectingValue)) {
            throw new JsonException("Can't add a value here");
          }

          break;
      }
    }

    private void Init() {
      this.has_reached_end = false;
      this.hex_seq = new Char[4];
      this.indentation = 0;
      this.indent_value = 4;
      this.PrettyPrint = false;
      this.Validate = true;
      this.LowerCaseProperties = false;

      this.ctx_stack = new Stack<WriterContext>();
      this.context = new WriterContext();
      this.ctx_stack.Push(this.context);
    }

    private static void IntToHex(Int32 n, Char[] hex) {
      Int32 num;
      for (Int32 i = 0; i < 4; i++) {
        num = n % 16;
        hex[3 - i] = num < 10 ? (Char)('0' + num) : (Char)('A' + (num - 10));
        n >>= 4;
      }
    }

    private void Indent() {
      if (this.PrettyPrint) {
        this.indentation += this.indent_value;
      }
    }

    private void Put(String str) {
      if (this.PrettyPrint && !this.context.ExpectingValue) {
        for (Int32 i = 0; i < this.indentation; i++) {
          this.TextWriter.Write(' ');
        }
      }
      this.TextWriter.Write(str);
    }

    private void PutNewline() => this.PutNewline(true);

    private void PutNewline(Boolean add_comma) {
      if (add_comma && !this.context.ExpectingValue && this.context.Count > 1) {
        this.TextWriter.Write(',');
      }
      if (this.PrettyPrint && !this.context.ExpectingValue) {
        this.TextWriter.Write(Environment.NewLine);
      }
    }

    private void PutString(String str) {
      this.Put(String.Empty);

      this.TextWriter.Write('"');

      Int32 n = str.Length;
      for (Int32 i = 0; i < n; i++) {
        switch (str[i]) {
          case '\n':
            this.TextWriter.Write("\\n");
            continue;

          case '\r':
            this.TextWriter.Write("\\r");
            continue;

          case '\t':
            this.TextWriter.Write("\\t");
            continue;

          case '"':
          case '\\':
            this.TextWriter.Write('\\');
            this.TextWriter.Write(str[i]);
            continue;

          case '\f':
            this.TextWriter.Write("\\f");
            continue;

          case '\b':
            this.TextWriter.Write("\\b");
            continue;
        }

        if (str[i] >= 32 && str[i] <= 126) {
          this.TextWriter.Write(str[i]);
          continue;
        }

        // Default, turn into a \uXXXX sequence
        IntToHex(str[i], this.hex_seq);
        this.TextWriter.Write("\\u");
        this.TextWriter.Write(this.hex_seq);
      }

      this.TextWriter.Write('"');
    }

    private void Unindent() {
      if (this.PrettyPrint) {
        this.indentation -= this.indent_value;
      }
    }
    #endregion

    public override String ToString() => this.inst_string_builder == null ? String.Empty : this.inst_string_builder.ToString();

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
      this.DoValidation(Condition.Value);
      this.PutNewline();

      this.Put(boolean ? "true" : "false");

      this.context.ExpectingValue = false;
    }

    public void Write(Decimal number) {
      this.DoValidation(Condition.Value);
      this.PutNewline();

      this.Put(Convert.ToString(number, number_format));

      this.context.ExpectingValue = false;
    }

    public void Write(Double number) {
      this.DoValidation(Condition.Value);
      this.PutNewline();

      String str = Convert.ToString(number, number_format);
      this.Put(str);

      if (str.IndexOf('.') == -1 && str.IndexOf('E') == -1) {
        this.TextWriter.Write(".0");
      }

      this.context.ExpectingValue = false;
    }

    public void Write(Int32 number) {
      this.DoValidation(Condition.Value);
      this.PutNewline();

      this.Put(Convert.ToString(number, number_format));

      this.context.ExpectingValue = false;
    }

    public void Write(Int64 number) {
      this.DoValidation(Condition.Value);
      this.PutNewline();

      this.Put(Convert.ToString(number, number_format));

      this.context.ExpectingValue = false;
    }

    public void Write(String str) {
      this.DoValidation(Condition.Value);
      this.PutNewline();

      if (str == null) {
        this.Put("null");
      } else {
        this.PutString(str);
      }

      this.context.ExpectingValue = false;
    }

    public void Write(UInt64 number) {
      this.DoValidation(Condition.Value);
      this.PutNewline();

      this.Put(Convert.ToString(number, number_format));

      this.context.ExpectingValue = false;
    }

    public void WriteArrayEnd() {
      this.DoValidation(Condition.InArray);
      this.PutNewline(false);

      this.ctx_stack.Pop();
      if (this.ctx_stack.Count == 1) {
        this.has_reached_end = true;
      } else {
        this.context = this.ctx_stack.Peek();
        this.context.ExpectingValue = false;
      }

      this.Unindent();
      this.Put("]");
    }

    public void WriteArrayStart() {
      this.DoValidation(Condition.NotAProperty);
      this.PutNewline();

      this.Put("[");

      this.context = new WriterContext {
        InArray = true
      };
      this.ctx_stack.Push(this.context);

      this.Indent();
    }

    public void WriteObjectEnd() {
      this.DoValidation(Condition.InObject);
      this.PutNewline(false);

      this.ctx_stack.Pop();
      if (this.ctx_stack.Count == 1) {
        this.has_reached_end = true;
      } else {
        this.context = this.ctx_stack.Peek();
        this.context.ExpectingValue = false;
      }

      this.Unindent();
      this.Put("}");
    }

    public void WriteObjectStart() {
      this.DoValidation(Condition.NotAProperty);
      this.PutNewline();

      this.Put("{");

      this.context = new WriterContext {
        InObject = true
      };
      this.ctx_stack.Push(this.context);

      this.Indent();
    }

    public void WritePropertyName(String property_name) {
      this.DoValidation(Condition.Property);
      this.PutNewline();
      String propertyName = (property_name == null || !this.LowerCaseProperties) ? property_name : property_name.ToLowerInvariant();

      this.PutString(propertyName);

      if (this.PrettyPrint) {
        if (propertyName.Length > this.context.Padding) {
          this.context.Padding = propertyName.Length;
        }

        for (Int32 i = this.context.Padding - propertyName.Length;
                     i >= 0; i--) {
          this.TextWriter.Write(' ');
        }

        this.TextWriter.Write(": ");
      } else {
        this.TextWriter.Write(':');
      }

      this.context.ExpectingValue = true;
    }
  }
}