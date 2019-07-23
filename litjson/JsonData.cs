#region Header
/**
 * JsonData.cs
 *   Generic type to hold JSON data (objects, arrays, and so on). This is
 *   the default type returned by JsonMapper.ToObject().
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 **/
#endregion


using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;


namespace LitJson
{
  public class JsonData : IJsonWrapper, IEquatable<JsonData> {
    #region Fields
    private IList<JsonData> inst_array;
    private Boolean inst_boolean;
    private Double inst_double;
    private Int32 inst_int;
    private Int64 inst_long;
    private IDictionary<String, JsonData> inst_object;
    private String inst_string;
    private String json;
    private JsonType type;

    // Used to implement the IOrderedDictionary interface
    private IList<KeyValuePair<String, JsonData>> object_list;
    #endregion

    #region Properties
    public Int32 Count => this.EnsureCollection().Count;

    public Boolean IsArray => this.type == JsonType.Array;

    public Boolean IsBoolean => this.type == JsonType.Boolean;

    public Boolean IsDouble => this.type == JsonType.Double;

    public Boolean IsInt => this.type == JsonType.Int;

    public Boolean IsLong => this.type == JsonType.Long;

    public Boolean IsObject => this.type == JsonType.Object;

    public Boolean IsString => this.type == JsonType.String;

    public ICollection<String> Keys  {
      get {
        _ = this.EnsureDictionary();
        return this.inst_object.Keys;
      }
    }

    /// <summary>
    /// Determines whether the dictionary contains an element that has the specified key.
    /// </summary>
    /// <param name="key">The key to locate in the dictionary.</param>
    /// <returns>true if the dictionary contains an element that has the specified key; otherwise, false.</returns>
    public Boolean ContainsKey(String key) {
      _ = this.EnsureDictionary();
      return this.inst_object.Keys.Contains(key);
    }
    #endregion

    #region ICollection Properties
    Int32 ICollection.Count => this.Count;

    Boolean ICollection.IsSynchronized => this.EnsureCollection().IsSynchronized;

    Object ICollection.SyncRoot => this.EnsureCollection().SyncRoot;
    #endregion

    #region IDictionary Properties
    Boolean IDictionary.IsFixedSize => this.EnsureDictionary().IsFixedSize;

    Boolean IDictionary.IsReadOnly => this.EnsureDictionary().IsReadOnly;

    ICollection IDictionary.Keys {
      get {
        _ = this.EnsureDictionary();
        IList<String> keys = new List<String>();

        foreach (KeyValuePair<String, JsonData> entry in this.object_list) {
          keys.Add(entry.Key);
        }

        return (ICollection)keys;
      }
    }

    ICollection IDictionary.Values {
      get {
        _ = this.EnsureDictionary();
        IList<JsonData> values = new List<JsonData>();

        foreach (KeyValuePair<String, JsonData> entry in this.object_list) {
          values.Add(entry.Value);
        }

        return (ICollection)values;
      }
    }
    #endregion

    #region IJsonWrapper Properties
    Boolean IJsonWrapper.IsArray => this.IsArray;

    Boolean IJsonWrapper.IsBoolean => this.IsBoolean;

    Boolean IJsonWrapper.IsDouble => this.IsDouble;

    Boolean IJsonWrapper.IsInt => this.IsInt;

    Boolean IJsonWrapper.IsLong => this.IsLong;

    Boolean IJsonWrapper.IsObject => this.IsObject;

    Boolean IJsonWrapper.IsString => this.IsString;
    #endregion

    #region IList Properties
    Boolean IList.IsFixedSize => this.EnsureList().IsFixedSize;

    Boolean IList.IsReadOnly => this.EnsureList().IsReadOnly;
    #endregion

    #region IDictionary Indexer
    Object IDictionary.this[Object key] {
      get => this.EnsureDictionary()[key];
      set {
        if (!(key is String)) {
          throw new ArgumentException("The key has to be a string");
        }

        JsonData data = this.ToJsonData(value);

        this[(String)key] = data;
      }
    }
    #endregion

    #region IOrderedDictionary Indexer
    Object IOrderedDictionary.this[Int32 idx] {
      get {
        _ = this.EnsureDictionary();
        return this.object_list[idx].Value;
      }
      set {
        _ = this.EnsureDictionary();
        JsonData data = this.ToJsonData(value);

        KeyValuePair<String, JsonData> old_entry = this.object_list[idx];

        this.inst_object[old_entry.Key] = data;

        KeyValuePair<String, JsonData> entry = new KeyValuePair<String, JsonData>(old_entry.Key, data);

        this.object_list[idx] = entry;
      }
    }
    #endregion

    #region IList Indexer
    Object IList.this[Int32 index] {
      get => this.EnsureList()[index];

      set {
        _ = this.EnsureList();
        this[index] = this.ToJsonData(value);
      }
    }
    #endregion

    #region Public Indexers
    public JsonData this[String prop_name] {
      get {
        _ = this.EnsureDictionary();
        return this.inst_object[prop_name];
      }

      set {
        _ = this.EnsureDictionary();

        KeyValuePair<String, JsonData> entry = new KeyValuePair<String, JsonData>(prop_name, value);

        if (this.inst_object.ContainsKey(prop_name)) {
          for (Int32 i = 0; i < this.object_list.Count; i++) {
            if (this.object_list[i].Key == prop_name) {
              this.object_list[i] = entry;
              break;
            }
          }
        } else {
          this.object_list.Add(entry);
        }

        this.inst_object[prop_name] = value;

        this.json = null;
      }
    }

    public JsonData this[Int32 index] {
      get {
        _ = this.EnsureCollection();
        return this.type == JsonType.Array ? this.inst_array[index] : this.object_list[index].Value;
      }

      set {
        _ = this.EnsureCollection();

        if (this.type == JsonType.Array) {
          this.inst_array[index] = value;
        } else {
          KeyValuePair<String, JsonData> entry = this.object_list[index];
          KeyValuePair<String, JsonData> new_entry = new KeyValuePair<String, JsonData>(entry.Key, value);

          this.object_list[index] = new_entry;
          this.inst_object[entry.Key] = value;
        }

        this.json = null;
      }
    }
    #endregion

    #region Constructors
    public JsonData() { }

    public JsonData(Boolean boolean) {
      this.type = JsonType.Boolean;
      this.inst_boolean = boolean;
    }

    public JsonData(Double number) {
      this.type = JsonType.Double;
      this.inst_double = number;
    }

    public JsonData(Int32 number) {
      this.type = JsonType.Int;
      this.inst_int = number;
    }

    public JsonData(Int64 number) {
      this.type = JsonType.Long;
      this.inst_long = number;
    }

    public JsonData(Object obj) {
      if (obj is Boolean) {
        this.type = JsonType.Boolean;
        this.inst_boolean = (Boolean)obj;
        return;
      }

      if (obj is Double) {
        this.type = JsonType.Double;
        this.inst_double = (Double)obj;
        return;
      }

      if (obj is Int32) {
        this.type = JsonType.Int;
        this.inst_int = (Int32)obj;
        return;
      }

      if (obj is Int64) {
        this.type = JsonType.Long;
        this.inst_long = (Int64)obj;
        return;
      }

      if (obj is String) {
        this.type = JsonType.String;
        this.inst_string = (String)obj;
        return;
      }

      throw new ArgumentException("Unable to wrap the given object with JsonData");
    }

    public JsonData(String str) {
      this.type = JsonType.String;
      this.inst_string = str;
    }
    #endregion

    #region Implicit Conversions
    public static implicit operator JsonData(Boolean data) => new JsonData(data);

    public static implicit operator JsonData(Double data) => new JsonData(data);

    public static implicit operator JsonData(Int32 data) => new JsonData(data);

    public static implicit operator JsonData(Int64 data) => new JsonData(data);

    public static implicit operator JsonData(String data) => new JsonData(data);
    #endregion

    #region Explicit Conversions
    public static explicit operator Boolean(JsonData data) {
      if (data.type != JsonType.Boolean) {
        throw new InvalidCastException("Instance of JsonData doesn't hold a double");
      }
      return data.inst_boolean;
    }

    public static explicit operator Double(JsonData data) {
      if(data.type != JsonType.Double) {
        throw new InvalidCastException("Instance of JsonData doesn't hold a double");
      }
      return data.inst_double;
    }

    public static explicit operator Int32(JsonData data) {
      if (data.type != JsonType.Int) {
        throw new InvalidCastException("Instance of JsonData doesn't hold an int");
      }
      return data.inst_int;
    }

    public static explicit operator Int64(JsonData data) {
      if (data.type != JsonType.Long && data.type != JsonType.Int) {
        throw new InvalidCastException("Instance of JsonData doesn't hold an int");
      }
      return (data.type == JsonType.Long) ? data.inst_long : data.inst_int;
    }

    public static explicit operator String(JsonData data) {
      if (data.type != JsonType.String) {
        throw new InvalidCastException("Instance of JsonData doesn't hold a string");
      }
      return data.inst_string;
    }
    #endregion

    #region ICollection Methods
    void ICollection.CopyTo(Array array, Int32 index) => this.EnsureCollection().CopyTo(array, index);
    #endregion

    #region IDictionary Methods
    void IDictionary.Add(Object key, Object value) {
      JsonData data = this.ToJsonData(value);

      this.EnsureDictionary().Add(key, data);

      KeyValuePair<String, JsonData> entry = new KeyValuePair<String, JsonData>((String)key, data);
      this.object_list.Add(entry);

      this.json = null;
    }

    void IDictionary.Clear() {
      this.EnsureDictionary().Clear();
      this.object_list.Clear();
      this.json = null;
    }

    Boolean IDictionary.Contains(Object key) => this.EnsureDictionary().Contains(key);

    IDictionaryEnumerator IDictionary.GetEnumerator() => ((IOrderedDictionary)this).GetEnumerator();

    void IDictionary.Remove(Object key) {
      this.EnsureDictionary().Remove(key);

      for (Int32 i = 0; i < this.object_list.Count; i++) {
        if (this.object_list[i].Key == (String)key) {
          this.object_list.RemoveAt(i);
          break;
        }
      }

      this.json = null;
    }
    #endregion

    #region IEnumerable Methods
    IEnumerator IEnumerable.GetEnumerator() => this.EnsureCollection().GetEnumerator();
    #endregion

    #region IJsonWrapper Methods
    Boolean IJsonWrapper.GetBoolean() {
      if (this.type != JsonType.Boolean) {
        throw new InvalidOperationException("JsonData instance doesn't hold a boolean");
      }

      return this.inst_boolean;
    }

    Double IJsonWrapper.GetDouble() {
      if (this.type != JsonType.Double) {
        throw new InvalidOperationException("JsonData instance doesn't hold a double");
      }

      return this.inst_double;
    }

    Int32 IJsonWrapper.GetInt() {
      if (this.type != JsonType.Int) {
        throw new InvalidOperationException("JsonData instance doesn't hold an int");
      }

      return this.inst_int;
    }

    Int64 IJsonWrapper.GetLong() {
      if(this.type != JsonType.Long) {
        throw new InvalidOperationException("JsonData instance doesn't hold a long");
      }

      return this.inst_long;
    }

    String IJsonWrapper.GetString() {
      if (this.type != JsonType.String) {
        throw new InvalidOperationException("JsonData instance doesn't hold a string");
      }

      return this.inst_string;
    }

    void IJsonWrapper.SetBoolean(Boolean val) {
      this.type = JsonType.Boolean;
      this.inst_boolean = val;
      this.json = null;
    }

    void IJsonWrapper.SetDouble(Double val) {
      this.type = JsonType.Double;
      this.inst_double = val;
      this.json = null;
    }

    void IJsonWrapper.SetInt(Int32 val) {
      this.type = JsonType.Int;
      this.inst_int = val;
      this.json = null;
    }

    void IJsonWrapper.SetLong(Int64 val) {
      this.type = JsonType.Long;
      this.inst_long = val;
      this.json = null;
    }

    void IJsonWrapper.SetString(String val) {
      this.type = JsonType.String;
      this.inst_string = val;
      this.json = null;
    }

    String IJsonWrapper.ToJson() => this.ToJson();

    void IJsonWrapper.ToJson(JsonWriter writer) => this.ToJson(writer);
    #endregion

    #region IList Methods
    Int32 IList.Add(Object value) => this.Add(value);

    void IList.Clear() {
      this.EnsureList().Clear();
      this.json = null;
    }

    Boolean IList.Contains(Object value) => this.EnsureList().Contains(value);

    Int32 IList.IndexOf(Object value) => this.EnsureList().IndexOf(value);

    void IList.Insert(Int32 index, Object value) {
      this.EnsureList().Insert(index, value);
      this.json = null;
    }

    void IList.Remove(Object value) {
      this.EnsureList().Remove(value);
      this.json = null;
    }

    void IList.RemoveAt(Int32 index) {
      this.EnsureList().RemoveAt(index);
      this.json = null;
    }
    #endregion

    #region IOrderedDictionary Methods
    IDictionaryEnumerator IOrderedDictionary.GetEnumerator() {
      _ = this.EnsureDictionary();
      return new OrderedDictionaryEnumerator(this.object_list.GetEnumerator());
    }

    void IOrderedDictionary.Insert(Int32 idx, Object key, Object value) {
      String property = (String)key;
      JsonData data = this.ToJsonData(value);

      this[property] = data;

      KeyValuePair<String, JsonData> entry = new KeyValuePair<String, JsonData>(property, data);

      this.object_list.Insert(idx, entry);
    }

    void IOrderedDictionary.RemoveAt(Int32 idx) {
      _ = this.EnsureDictionary();
      _ = this.inst_object.Remove(this.object_list[idx].Key);
      this.object_list.RemoveAt(idx);
    }
    #endregion

    #region Private Methods
    private ICollection EnsureCollection() {
      if (this.type == JsonType.Array) {
        return (ICollection)this.inst_array;
      }

      if (this.type == JsonType.Object) {
        return (ICollection)this.inst_object;
      }

      throw new InvalidOperationException("The JsonData instance has to be initialized first");
    }

    private IDictionary EnsureDictionary() {
      if (this.type == JsonType.Object) {
        return (IDictionary)this.inst_object;
      }

      if (this.type != JsonType.None) {
        throw new InvalidOperationException("Instance of JsonData is not a dictionary");
      }

      this.type = JsonType.Object;
      this.inst_object = new Dictionary<String, JsonData>();
      this.object_list = new List<KeyValuePair<String, JsonData>>();

      return (IDictionary)this.inst_object;
    }

    private IList EnsureList() {
      if(this.type == JsonType.Array) {
        return (IList)this.inst_array;
      }

      if (this.type != JsonType.None) {
        throw new InvalidOperationException("Instance of JsonData is not a list");
      }

      this.type = JsonType.Array;
      this.inst_array = new List<JsonData>();

      return (IList)this.inst_array;
    }

    private JsonData ToJsonData(Object obj) => obj == null ? null : obj is JsonData ? (JsonData)obj : new JsonData(obj);

    private static void WriteJson(IJsonWrapper obj, JsonWriter writer)
    {
      if (obj == null) {
        writer.Write(null);
        return;
      }

      if (obj.IsString) {
        writer.Write(obj.GetString());
        return;
      }

      if (obj.IsBoolean) {
        writer.Write(obj.GetBoolean());
        return;
      }

      if (obj.IsDouble) {
        writer.Write(obj.GetDouble());
        return;
      }

      if (obj.IsInt) {
        writer.Write(obj.GetInt());
        return;
      }

      if (obj.IsLong) {
        writer.Write(obj.GetLong());
        return;
      }

      if (obj.IsArray) {
        writer.WriteArrayStart();
        foreach (Object elem in (IList)obj) {
          WriteJson((JsonData)elem, writer);
        }

        writer.WriteArrayEnd();

        return;
      }

      if (obj.IsObject) {
        writer.WriteObjectStart();

        foreach (DictionaryEntry entry in (IDictionary)obj) {
          writer.WritePropertyName((String)entry.Key);
          WriteJson((JsonData)entry.Value, writer);
        }
        writer.WriteObjectEnd();

        return;
      }
    }
    #endregion

    public Int32 Add(Object value) {
      JsonData data = this.ToJsonData(value);
      this.json = null;
      return this.EnsureList().Add(data);
    }

    public void Clear() {
      if (this.IsObject) {
        ((IDictionary)this).Clear();
        return;
      }

      if (this.IsArray) {
        ((IList)this).Clear();
        return;
      }
    }

    public Boolean Equals(JsonData x) {
      if (x == null) {
        return false;
      }

      if (x.type != this.type) {
        return false;
      }

      switch (this.type) {
        case JsonType.None:
          return true;
        case JsonType.Object:
          return this.inst_object.Equals(x.inst_object);
        case JsonType.Array:
          return this.inst_array.Equals(x.inst_array);
        case JsonType.String:
          return this.inst_string.Equals(x.inst_string);
        case JsonType.Int:
          return this.inst_int.Equals(x.inst_int);
        case JsonType.Long:
          return this.inst_long.Equals(x.inst_long);
        case JsonType.Double:
          return this.inst_double.Equals(x.inst_double);
        case JsonType.Boolean:
          return this.inst_boolean.Equals(x.inst_boolean);
      }

      return false;
    }

    public JsonType GetJsonType() => this.type;

    public void SetJsonType(JsonType type) {
      if (this.type == type) {
        return;
      }

      switch (type) {
        case JsonType.None:
          break;

        case JsonType.Object:
          this.inst_object = new Dictionary<String, JsonData>();
          this.object_list = new List<KeyValuePair<String, JsonData>>();
          break;

        case JsonType.Array:
          this.inst_array = new List<JsonData>();
          break;

        case JsonType.String:
          this.inst_string = default;
          break;

        case JsonType.Int:
          this.inst_int = default;
          break;

        case JsonType.Long:
          this.inst_long = default;
          break;

        case JsonType.Double:
          this.inst_double = default;
          break;

        case JsonType.Boolean:
          this.inst_boolean = default;
          break;
      }

      this.type = type;
    }

    public String ToJson() {
      if (this.json != null) {
        return this.json;
      }

      StringWriter sw = new StringWriter();
      JsonWriter writer = new JsonWriter(sw) {
        Validate = false
      };

      WriteJson(this, writer);
      this.json = sw.ToString();

      return this.json;
    }

    public void ToJson(JsonWriter writer) {
      Boolean old_validate = writer.Validate;

      writer.Validate = false;

      WriteJson(this, writer);

      writer.Validate = old_validate;
    }

    public override String ToString() {
      switch (this.type) {
        case JsonType.Array:
          return "JsonData array";

        case JsonType.Boolean:
          return this.inst_boolean.ToString();

        case JsonType.Double:
          return this.inst_double.ToString();

        case JsonType.Int:
          return this.inst_int.ToString();

        case JsonType.Long:
          return this.inst_long.ToString();

        case JsonType.Object:
          return "JsonData object";

        case JsonType.String:
          return this.inst_string;
      }

      return "Uninitialized JsonData";
    }
  }

  internal class OrderedDictionaryEnumerator : IDictionaryEnumerator {
    readonly IEnumerator<KeyValuePair<String, JsonData>> list_enumerator;

    public Object Current => this.Entry;

    public DictionaryEntry Entry => new DictionaryEntry(this.Key, this.Value);

    public Object Key => this.list_enumerator.Current.Key;

    public Object Value => this.list_enumerator.Current.Value;

    public OrderedDictionaryEnumerator(IEnumerator<KeyValuePair<String, JsonData>> enumerator) => this.list_enumerator = enumerator;

    public Boolean MoveNext() => this.list_enumerator.MoveNext();

    public void Reset() => this.list_enumerator.Reset();
  }
}