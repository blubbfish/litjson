#region Header
/**
 * IJsonWrapper.cs
 *   Interface that represents a type capable of handling all kinds of JSON
 *   data. This is mainly used when mapping objects through JsonMapper, and
 *   it's implemented by JsonData.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 **/
#endregion


using System;
using System.Collections;
using System.Collections.Specialized;


namespace LitJson {
  public enum JsonType {
    None,
    Object,
    Array,
    String,
    Int,
    Long,
    Double,
    Boolean
  }

  public interface IJsonWrapper : IList, IOrderedDictionary {
    Boolean IsArray { get; }
    Boolean IsBoolean { get; }
    Boolean IsDouble { get; }
    Boolean IsInt { get; }
    Boolean IsLong { get; }
    Boolean IsObject { get; }
    Boolean IsString { get; }

    Boolean GetBoolean();
    Double GetDouble();
    Int32 GetInt();
    JsonType GetJsonType();
    Int64 GetLong();
    String GetString();

    void SetBoolean(Boolean val);
    void SetDouble(Double val);
    void SetInt(Int32 val);
    void SetJsonType(JsonType type);
    void SetLong(Int64 val);
    void SetString(String val);

    String ToJson();
    void ToJson(JsonWriter writer);
  }
}
