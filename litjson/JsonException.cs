#region Header
/**
 * JsonException.cs
 *   Base class throwed by LitJSON when a parsing error occurs.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 **/
#endregion


using System;


namespace LitJson {
  public class JsonException :
#if NETSTANDARD1_5
        Exception
#else
        ApplicationException
#endif
    {
    public JsonException() : base() { }

    internal JsonException(ParserToken token) : base(String.Format("Invalid token '{0}' in input string", token)) { }

    internal JsonException(ParserToken token, Exception inner_exception) : base(String.Format("Invalid token '{0}' in input string", token), inner_exception) { }

    internal JsonException(Int32 c) : base(String.Format("Invalid character '{0}' in input string", (Char)c)) { }

    internal JsonException(Int32 c, Exception inner_exception) : base(String.Format("Invalid character '{0}' in input string", (Char)c), inner_exception) { }

    public JsonException(String message) : base(message) { }

    public JsonException(String message, Exception inner_exception) : base(message, inner_exception) { }
  }
}
