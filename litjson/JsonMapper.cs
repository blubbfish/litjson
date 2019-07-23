#region Header
/**
 * JsonMapper.cs
 *   JSON to .Net object and object to JSON conversions.
 *
 * The authors disclaim copyright to this source code. For more details, see
 * the COPYING file included with this distribution.
 **/
#endregion


using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;


namespace LitJson {
  internal struct PropertyMetadata {
    public MemberInfo Info;
    public Boolean IsField;
    public Type Type;
  }

  internal struct ArrayMetadata {
    private Type element_type;

    public Type ElementType {
      get => this.element_type ?? typeof(JsonData);
      set => this.element_type = value;
    }

    public Boolean IsArray { get; set; }

    public Boolean IsList { get; set; }
  }

  internal struct ObjectMetadata {
    private Type element_type;

    public Type ElementType {
      get => this.element_type ?? typeof(JsonData);
      set => this.element_type = value;
    }

    public Boolean IsDictionary { get; set; }

    public IDictionary<String, PropertyMetadata> Properties { get; set; }
  }

  internal delegate void ExporterFunc(Object obj, JsonWriter writer);
  public delegate void ExporterFunc<T>(T obj, JsonWriter writer);

  internal delegate Object ImporterFunc(Object input);
  public delegate TValue ImporterFunc<TJson, TValue>(TJson input);

  public delegate IJsonWrapper WrapperFactory();

  public class JsonMapper {
    #region Fields
    private static readonly Int32 max_nesting_depth;

    private static readonly IFormatProvider datetime_format;

    private static readonly IDictionary<Type, ExporterFunc> base_exporters_table;
    private static readonly IDictionary<Type, ExporterFunc> custom_exporters_table;

    private static readonly IDictionary<Type, IDictionary<Type, ImporterFunc>> base_importers_table;
    private static readonly IDictionary<Type, IDictionary<Type, ImporterFunc>> custom_importers_table;

    private static readonly IDictionary<Type, ArrayMetadata> array_metadata;
    private static readonly Object array_metadata_lock = new Object();

    private static readonly IDictionary<Type, IDictionary<Type, MethodInfo>> conv_ops;
    private static readonly Object conv_ops_lock = new Object();

    private static readonly IDictionary<Type, ObjectMetadata> object_metadata;
    private static readonly Object object_metadata_lock = new Object();

    private static readonly IDictionary<Type, IList<PropertyMetadata>> type_properties;
    private static readonly Object type_properties_lock = new Object();

    private static readonly JsonWriter static_writer;
    private static readonly Object static_writer_lock = new Object();
    #endregion

    #region Constructors
    static JsonMapper() {
      max_nesting_depth = 100;

      array_metadata = new Dictionary<Type, ArrayMetadata>();
      conv_ops = new Dictionary<Type, IDictionary<Type, MethodInfo>>();
      object_metadata = new Dictionary<Type, ObjectMetadata>();
      type_properties = new Dictionary<Type, IList<PropertyMetadata>>();

      static_writer = new JsonWriter();

      datetime_format = DateTimeFormatInfo.InvariantInfo;

      base_exporters_table = new Dictionary<Type, ExporterFunc>();
      custom_exporters_table = new Dictionary<Type, ExporterFunc>();

      base_importers_table = new Dictionary<Type, IDictionary<Type, ImporterFunc>>();
      custom_importers_table = new Dictionary<Type, IDictionary<Type, ImporterFunc>>();

      RegisterBaseExporters();
      RegisterBaseImporters();
    }
    #endregion

    #region Private Methods
    private static void AddArrayMetadata(Type type) {
      if (array_metadata.ContainsKey(type)) {
        return;
      }

      ArrayMetadata data = new ArrayMetadata {
        IsArray = type.IsArray
      };

      if (type.GetInterface("System.Collections.IList") != null) {
        data.IsList = true;
      }

      foreach (PropertyInfo p_info in type.GetProperties()) {
        if (p_info.Name != "Item") {
          continue;
        }

        ParameterInfo[] parameters = p_info.GetIndexParameters();

        if (parameters.Length != 1) {
          continue;
        }

        if (parameters[0].ParameterType == typeof(Int32)) {
          data.ElementType = p_info.PropertyType;
        }
      }

      lock (array_metadata_lock) {
        try {
          array_metadata.Add(type, data);
        } catch (ArgumentException) {
          return;
        }
      }
    }

    private static void AddObjectMetadata(Type type) {
      if (object_metadata.ContainsKey(type)) {
        return;
      }

      ObjectMetadata data = new ObjectMetadata();

      if (type.GetInterface("System.Collections.IDictionary") != null) {
        data.IsDictionary = true;
      }

      data.Properties = new Dictionary<String, PropertyMetadata>();

      foreach (PropertyInfo p_info in type.GetProperties()) {
        if (p_info.Name == "Item") {
          ParameterInfo[] parameters = p_info.GetIndexParameters();

          if (parameters.Length != 1) {
            continue;
          }

          if (parameters[0].ParameterType == typeof(String)) {
            data.ElementType = p_info.PropertyType;
          }

          continue;
        }

        PropertyMetadata p_data = new PropertyMetadata {
          Info = p_info,
          Type = p_info.PropertyType
        };

        data.Properties.Add(p_info.Name, p_data);
      }

      foreach (FieldInfo f_info in type.GetFields()) {
        PropertyMetadata p_data = new PropertyMetadata {
          Info = f_info,
          IsField = true,
          Type = f_info.FieldType
        };

        data.Properties.Add(f_info.Name, p_data);
      }

      lock (object_metadata_lock) {
        try {
          object_metadata.Add(type, data);
        } catch (ArgumentException) {
          return;
        }
      }
    }

    private static void AddTypeProperties(Type type) {
      if (type_properties.ContainsKey(type)) {
        return;
      }

      IList<PropertyMetadata> props = new List<PropertyMetadata>();

      foreach (PropertyInfo p_info in type.GetProperties()) {
        if (p_info.Name == "Item") {
          continue;
        }

        PropertyMetadata p_data = new PropertyMetadata {
          Info = p_info,
          IsField = false
        };
        props.Add(p_data);
      }

      foreach (FieldInfo f_info in type.GetFields()) {
        PropertyMetadata p_data = new PropertyMetadata {
          Info = f_info,
          IsField = true
        };

        props.Add(p_data);
      }

      lock (type_properties_lock) {
        try {
          type_properties.Add(type, props);
        } catch (ArgumentException) {
          return;
        }
      }
    }

    private static MethodInfo GetConvOp(Type t1, Type t2) {
      lock (conv_ops_lock) {
        if (!conv_ops.ContainsKey(t1)) {
          conv_ops.Add(t1, new Dictionary<Type, MethodInfo>());
        }
      }

      if (conv_ops[t1].ContainsKey(t2)) {
        return conv_ops[t1][t2];
      }

      MethodInfo op = t1.GetMethod("op_Implicit", new Type[] { t2 });

      lock (conv_ops_lock) {
        try {
          conv_ops[t1].Add(t2, op);
        } catch (ArgumentException) {
          return conv_ops[t1][t2];
        }
      }

      return op;
    }

    private static Object ReadValue(Type inst_type, JsonReader reader) {
      reader.Read();

      if (reader.Token == JsonToken.ArrayEnd) {
        return null;
      }

      Type underlying_type = Nullable.GetUnderlyingType(inst_type);
      Type value_type = underlying_type ?? inst_type;

      if (reader.Token == JsonToken.Null) {
        #if NETSTANDARD1_5
          if (inst_type.IsClass() || underlying_type != null) {
            return null;
          }
        #else
          if (inst_type.IsClass || underlying_type != null) {
            return null;
          }
        #endif

        throw new JsonException(String.Format("Can't assign null to an instance of type {0}", inst_type));
      }

      if (reader.Token == JsonToken.Double ||
          reader.Token == JsonToken.Int ||
          reader.Token == JsonToken.Long ||
          reader.Token == JsonToken.String ||
          reader.Token == JsonToken.Boolean) {

        Type json_type = reader.Value.GetType();

        if (value_type.IsAssignableFrom(json_type)) {
          return reader.Value;
        }

        // If there's a custom importer that fits, use it
        if (custom_importers_table.ContainsKey(json_type) &&
            custom_importers_table[json_type].ContainsKey(
                value_type)) {

          ImporterFunc importer =
              custom_importers_table[json_type][value_type];

          return importer(reader.Value);
        }

        // Maybe there's a base importer that works
        if (base_importers_table.ContainsKey(json_type) &&
            base_importers_table[json_type].ContainsKey(
                value_type)) {

          ImporterFunc importer =
              base_importers_table[json_type][value_type];

          return importer(reader.Value);
        }

        // Maybe it's an enum
        #if NETSTANDARD1_5
          if (value_type.IsEnum()) {
            return Enum.ToObject (value_type, reader.Value);
          }
        #else
          if (value_type.IsEnum) {
            return Enum.ToObject(value_type, reader.Value);
          }
        #endif
        // Try using an implicit conversion operator
        MethodInfo conv_op = GetConvOp(value_type, json_type);

        if (conv_op != null) {
          return conv_op.Invoke(null, new Object[] { reader.Value });
        }

        // No luck
        throw new JsonException(String.Format("Can't assign value '{0}' (type {1}) to type {2}", reader.Value, json_type, inst_type));
      }

      Object instance = null;

      if (reader.Token == JsonToken.ArrayStart) {

        AddArrayMetadata(inst_type);
        ArrayMetadata t_data = array_metadata[inst_type];

        if (!t_data.IsArray && !t_data.IsList) {
          throw new JsonException(String.Format("Type {0} can't act as an array", inst_type));
        }

        IList list;
        Type elem_type;

        if (!t_data.IsArray) {
          list = (IList)Activator.CreateInstance(inst_type);
          elem_type = t_data.ElementType;
        } else {
          list = new ArrayList();
          elem_type = inst_type.GetElementType();
        }

        while (true) {
          Object item = ReadValue(elem_type, reader);
          if (item == null && reader.Token == JsonToken.ArrayEnd) {
            break;
          }

          list.Add(item);
        }

        if (t_data.IsArray) {
          Int32 n = list.Count;
          instance = Array.CreateInstance(elem_type, n);

          for (Int32 i = 0; i < n; i++) {
            ((Array)instance).SetValue(list[i], i);
          }
        } else {
          instance = list;
        }
      } else if (reader.Token == JsonToken.ObjectStart) {
        AddObjectMetadata(value_type);
        ObjectMetadata t_data = object_metadata[value_type];

        instance = Activator.CreateInstance(value_type);

        while (true) {
          reader.Read();

          if (reader.Token == JsonToken.ObjectEnd) {
            break;
          }

          String property = (String)reader.Value;

          if (t_data.Properties.ContainsKey(property)) {
            PropertyMetadata prop_data = t_data.Properties[property];

            if (prop_data.IsField) {
              ((FieldInfo)prop_data.Info).SetValue(instance, ReadValue(prop_data.Type, reader));
            } else {
              PropertyInfo p_info = (PropertyInfo)prop_data.Info;

              if (p_info.CanWrite) {
                p_info.SetValue(instance, ReadValue(prop_data.Type, reader), null);
              } else {
                ReadValue(prop_data.Type, reader);
              }
            }

          } else {
            if (!t_data.IsDictionary) {

              if (!reader.SkipNonMembers) {
                throw new JsonException(String.Format("The type {0} doesn't have the property '{1}'", inst_type, property));
              } else {
                ReadSkip(reader);
                continue;
              }
            }
            ((IDictionary)instance).Add(property, ReadValue(t_data.ElementType, reader));
          }

        }

      }

      return instance;
    }

    private static IJsonWrapper ReadValue(WrapperFactory factory, JsonReader reader) {
      reader.Read();

      if (reader.Token == JsonToken.ArrayEnd ||
          reader.Token == JsonToken.Null) {
        return null;
      }

      IJsonWrapper instance = factory();

      if (reader.Token == JsonToken.String) {
        instance.SetString((String)reader.Value);
        return instance;
      }

      if (reader.Token == JsonToken.Double) {
        instance.SetDouble((Double)reader.Value);
        return instance;
      }

      if (reader.Token == JsonToken.Int) {
        instance.SetInt((Int32)reader.Value);
        return instance;
      }

      if (reader.Token == JsonToken.Long) {
        instance.SetLong((Int64)reader.Value);
        return instance;
      }

      if (reader.Token == JsonToken.Boolean) {
        instance.SetBoolean((Boolean)reader.Value);
        return instance;
      }

      if (reader.Token == JsonToken.ArrayStart) {
        instance.SetJsonType(JsonType.Array);

        while (true) {
          IJsonWrapper item = ReadValue(factory, reader);
          if (item == null && reader.Token == JsonToken.ArrayEnd) {
            break;
          }
          instance.Add(item);
        }
      } else if (reader.Token == JsonToken.ObjectStart) {
        instance.SetJsonType(JsonType.Object);

        while (true) {
          reader.Read();

          if (reader.Token == JsonToken.ObjectEnd) {
            break;
          }

          String property = (String)reader.Value;

          instance[property] = ReadValue(factory, reader);
        }

      }

      return instance;
    }

    private static void ReadSkip(JsonReader reader) => ToWrapper(delegate { return new JsonMockWrapper(); }, reader);

    private static void RegisterBaseExporters() {
      base_exporters_table[typeof(Byte)] = delegate (Object obj, JsonWriter writer) {
        writer.Write(Convert.ToInt32((Byte)obj));
      };

      base_exporters_table[typeof(Char)] = delegate (Object obj, JsonWriter writer) {
        writer.Write(Convert.ToString((Char)obj));
      };

      base_exporters_table[typeof(DateTime)] = delegate (Object obj, JsonWriter writer) {
        writer.Write(Convert.ToString((DateTime)obj, datetime_format));
      };
      base_exporters_table[typeof(TimeSpan)] = delegate (Object obj, JsonWriter writer) {
        writer.Write(Convert.ToString((TimeSpan)obj, datetime_format));
      };

      base_exporters_table[typeof(Decimal)] = delegate (Object obj, JsonWriter writer) {
        writer.Write((Decimal)obj);
      };

      base_exporters_table[typeof(SByte)] = delegate (Object obj, JsonWriter writer) {
        writer.Write(Convert.ToInt32((SByte)obj));
      };

      base_exporters_table[typeof(Int16)] = delegate (Object obj, JsonWriter writer) {
        writer.Write(Convert.ToInt32((Int16)obj));
      };

      base_exporters_table[typeof(UInt16)] = delegate (Object obj, JsonWriter writer) {
        writer.Write(Convert.ToInt32((UInt16)obj));
      };

      base_exporters_table[typeof(UInt32)] = delegate (Object obj, JsonWriter writer) {
        writer.Write(Convert.ToUInt64((UInt32)obj));
      };

      base_exporters_table[typeof(UInt64)] = delegate (Object obj, JsonWriter writer) {
        writer.Write((UInt64)obj);
      };
    }

    private static void RegisterBaseImporters() {
      ImporterFunc importer;

      importer = delegate (Object input) {
        return Convert.ToByte((Int32)input);
      };
      RegisterImporter(base_importers_table, typeof(Int32), typeof(Byte), importer);

      importer = delegate (Object input) {
        return Convert.ToUInt64((Int32)input);
      };
      RegisterImporter(base_importers_table, typeof(Int32), typeof(UInt64), importer);

      importer = delegate (Object input) {
        return Convert.ToSByte((Int32)input);
      };
      RegisterImporter(base_importers_table, typeof(Int32), typeof(SByte), importer);

      importer = delegate (Object input) {
        return Convert.ToInt16((Int32)input);
      };
      RegisterImporter(base_importers_table, typeof(Int32), typeof(Int16), importer);

      importer = delegate (Object input) {
        return Convert.ToUInt16((Int32)input);
      };
      RegisterImporter(base_importers_table, typeof(Int32), typeof(UInt16), importer);

      importer = delegate (Object input) {
        return Convert.ToUInt32((Int32)input);
      };
      RegisterImporter(base_importers_table, typeof(Int32), typeof(UInt32), importer);

      importer = delegate (Object input) {
        return Convert.ToSingle((Int32)input);
      };
      RegisterImporter(base_importers_table, typeof(Int32), typeof(Single), importer);

      importer = delegate (Object input) {
        return Convert.ToDouble((Int32)input);
      };
      RegisterImporter(base_importers_table, typeof(Int32), typeof(Double), importer);

      importer = delegate (Object input) {
        return Convert.ToDecimal((Double)input);
      };
      RegisterImporter(base_importers_table, typeof(Double), typeof(Decimal), importer);

      importer = delegate (Object input) {
        return Convert.ToUInt32((Int64)input);
      };
      RegisterImporter(base_importers_table, typeof(Int64), typeof(UInt32), importer);

      importer = delegate (Object input) {
        return Convert.ToChar((String)input);
      };
      RegisterImporter(base_importers_table, typeof(String), typeof(Char), importer);

      importer = delegate (Object input) {
        return Convert.ToDateTime((String)input, datetime_format);
      };
      RegisterImporter(base_importers_table, typeof(String), typeof(DateTime), importer);
    }

    private static void RegisterImporter(IDictionary<Type, IDictionary<Type, ImporterFunc>> table, Type json_type, Type value_type, ImporterFunc importer) {
      if (!table.ContainsKey(json_type)) {
        table.Add(json_type, new Dictionary<Type, ImporterFunc>());
      }

      table[json_type][value_type] = importer;
    }

    private static void WriteValue(Object obj, JsonWriter writer, Boolean writer_is_private, Int32 depth) {
      if (depth > max_nesting_depth) {
        throw new JsonException(String.Format("Max allowed object depth reached while trying to export from type {0}", obj.GetType()));
      }

      if (obj == null) {
        writer.Write(null);
        return;
      }

      if (obj is IJsonWrapper) {
        if (writer_is_private) {
          writer.TextWriter.Write(((IJsonWrapper)obj).ToJson());
        } else {
          ((IJsonWrapper)obj).ToJson(writer);
        }

        return;
      }

      if (obj is String) {
        writer.Write((String)obj);
        return;
      }

      if (obj is Double) {
        writer.Write((Double)obj);
        return;
      }

      if (obj is Int32) {
        writer.Write((Int32)obj);
        return;
      }

      if (obj is Boolean) {
        writer.Write((Boolean)obj);
        return;
      }

      if (obj is Int64) {
        writer.Write((Int64)obj);
        return;
      }

      if (obj is Array) {
        writer.WriteArrayStart();

        foreach (Object elem in (Array)obj) {
          WriteValue(elem, writer, writer_is_private, depth + 1);
        }

        writer.WriteArrayEnd();

        return;
      }

      if (obj is IList) {
        writer.WriteArrayStart();
        foreach (Object elem in (IList)obj) {
          WriteValue(elem, writer, writer_is_private, depth + 1);
        }

        writer.WriteArrayEnd();

        return;
      }

      if (obj is IDictionary) {
        writer.WriteObjectStart();
        foreach (DictionaryEntry entry in (IDictionary)obj) {
          writer.WritePropertyName((String)entry.Key);
          WriteValue(entry.Value, writer, writer_is_private,
                      depth + 1);
        }
        writer.WriteObjectEnd();

        return;
      }

      Type obj_type = obj.GetType();

      // See if there's a custom exporter for the object
      if (custom_exporters_table.ContainsKey(obj_type)) {
        ExporterFunc exporter = custom_exporters_table[obj_type];
        exporter(obj, writer);

        return;
      }

      // If not, maybe there's a base exporter
      if (base_exporters_table.ContainsKey(obj_type)) {
        ExporterFunc exporter = base_exporters_table[obj_type];
        exporter(obj, writer);

        return;
      }

      // Last option, let's see if it's an enum
      if (obj is Enum) {
        Type e_type = Enum.GetUnderlyingType(obj_type);

        if (e_type == typeof(Int64)
            || e_type == typeof(UInt32)
            || e_type == typeof(UInt64)) {
          writer.Write((UInt64)obj);
        } else {
          writer.Write((Int32)obj);
        }

        return;
      }

      // Okay, so it looks like the input should be exported as an
      // object
      AddTypeProperties(obj_type);
      IList<PropertyMetadata> props = type_properties[obj_type];

      writer.WriteObjectStart();
      foreach (PropertyMetadata p_data in props) {
        if (p_data.IsField) {
          writer.WritePropertyName(p_data.Info.Name);
          WriteValue(((FieldInfo)p_data.Info).GetValue(obj),
                      writer, writer_is_private, depth + 1);
        } else {
          PropertyInfo p_info = (PropertyInfo)p_data.Info;

          if (p_info.CanRead) {
            writer.WritePropertyName(p_data.Info.Name);
            WriteValue(p_info.GetValue(obj, null),
                        writer, writer_is_private, depth + 1);
          }
        }
      }
      writer.WriteObjectEnd();
    }
    #endregion

    public static String ToJson(Object obj) {
      lock (static_writer_lock) {
        static_writer.Reset();

        WriteValue(obj, static_writer, true, 0);

        return static_writer.ToString();
      }
    }

    public static void ToJson(Object obj, JsonWriter writer) => WriteValue(obj, writer, false, 0);

    public static JsonData ToObject(JsonReader reader) => (JsonData)ToWrapper(delegate { return new JsonData(); }, reader);

    public static JsonData ToObject(TextReader reader) => (JsonData)ToWrapper(delegate { return new JsonData(); }, new JsonReader(reader));

    public static JsonData ToObject(String json) => (JsonData)ToWrapper(delegate { return new JsonData(); }, json);

    public static T ToObject<T>(JsonReader reader) => (T)ReadValue(typeof(T), reader);

    public static T ToObject<T>(TextReader reader) => (T)ReadValue(typeof(T), new JsonReader(reader));

    public static T ToObject<T>(String json) => (T)ReadValue(typeof(T), new JsonReader(json));

    public static Object ToObject(String json, Type ConvertType) => ReadValue(ConvertType, new JsonReader(json));

    public static IJsonWrapper ToWrapper(WrapperFactory factory, JsonReader reader) => ReadValue(factory, reader);

    public static IJsonWrapper ToWrapper(WrapperFactory factory, String json) => ReadValue(factory, new JsonReader(json));

    public static void RegisterExporter<T>(ExporterFunc<T> exporter) => custom_exporters_table[typeof(T)] = (Object obj, JsonWriter writer) => { exporter((T)obj, writer); };

    public static void RegisterImporter<TJson, TValue>(ImporterFunc<TJson, TValue> importer) => RegisterImporter(custom_importers_table, typeof(TJson), typeof(TValue), (Object input) => { return importer((TJson)input); });

    public static void UnregisterExporters() => custom_exporters_table.Clear();

    public static void UnregisterImporters() => custom_importers_table.Clear();
  }
}
