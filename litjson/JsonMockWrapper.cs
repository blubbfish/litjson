#region Header
/**
 * JsonMockWrapper.cs
 *   Mock object implementing IJsonWrapper, to facilitate actions like
 *   skipping data more efficiently.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 **/
#endregion


using System;
using System.Collections;
using System.Collections.Specialized;


namespace LitJson {
  public class JsonMockWrapper : IJsonWrapper {
    public Boolean IsArray => false;

    public Boolean IsBoolean => false;

    public Boolean IsDouble => false;

    public Boolean IsInt => false;

    public Boolean IsLong => false;

    public Boolean IsObject => false;

    public Boolean IsString => false;

    public Boolean GetBoolean() => false;

    public Double GetDouble() => 0.0;

    public Int32 GetInt() => 0;

    public JsonType GetJsonType() => JsonType.None;

    public Int64 GetLong() => 0L;

    public String GetString() => "";

    public void SetBoolean(Boolean val) { }

    public void SetDouble(Double val) { }

    public void SetInt(Int32 val) { }

    public void SetJsonType(JsonType type) { }

    public void SetLong(Int64 val) { }

    public void SetString(String val) { }

    public String ToJson() => "";

    public void ToJson(JsonWriter writer) { }

    Boolean IList.IsFixedSize => true;

    Boolean IList.IsReadOnly => true;

    Object IList.this[Int32 index] {
      get => null;
      set {
      }
    }

    Int32 IList.Add(Object value) => 0;

    void IList.Clear() { }

    Boolean IList.Contains(Object value) => false;

    Int32 IList.IndexOf(Object value) => -1;

    void IList.Insert(Int32 i, Object v) { }

    void IList.Remove(Object value) { }

    void IList.RemoveAt(Int32 index) { }

    Int32 ICollection.Count => 0;

    Boolean ICollection.IsSynchronized => false;

    Object ICollection.SyncRoot => null;

    void ICollection.CopyTo(Array array, Int32 index) { }

    IEnumerator IEnumerable.GetEnumerator() => null;

    Boolean IDictionary.IsFixedSize => true;

    Boolean IDictionary.IsReadOnly => true;

    ICollection IDictionary.Keys => null;

    ICollection IDictionary.Values => null;

    Object IDictionary.this[Object key] {
      get => null;
      set {
      }
    }

    void IDictionary.Add(Object k, Object v) { }

    void IDictionary.Clear() { }

    Boolean IDictionary.Contains(Object key) => false;

    void IDictionary.Remove(Object key) { }

    IDictionaryEnumerator IDictionary.GetEnumerator() => null;

    Object IOrderedDictionary.this[Int32 idx] {
      get => null;
      set {
      }
    }

    IDictionaryEnumerator IOrderedDictionary.GetEnumerator() => null;

    void IOrderedDictionary.Insert(Int32 i, Object k, Object v) { }

    void IOrderedDictionary.RemoveAt(Int32 i) { }
  }
}
