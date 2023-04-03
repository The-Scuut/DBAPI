namespace DBAPI.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;
    using System.Text.RegularExpressions;
    using DBAPI.Library.Models;

    public class DBTypeConverter<T> : IDBTypeConverter where T : class
    {
        private readonly PropertyInfo[] _properties;
        public readonly string MySqlTypesString = "";

        public T Deserialize(string data, bool throwOnConversionError = true)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!data.StartsWith("{") || !data.EndsWith("}"))
                throw new ArgumentException("Data is not a valid JSON object");
            string[] values = data.Substring(1, data.Length - 2).Split(',');
            if (values.Length != _properties.Length && throwOnConversionError)
                throw new ArgumentException("Data does not match the number of properties");
            var instance = Activator.CreateInstance<T>();
            foreach (var kvp in values)
            {
                Regex splitRegex = new Regex(@"(?<!\d):(?!\d)|:(?!\d)|(?<!\d):"); // only split if not between numbers
                string[] split = splitRegex.Split(kvp);
                string key = split[0].Replace("\"", "");
                string value = split[1].Replace("\"", "");
                if (value == "null")
                    continue;
                var property = _properties.FirstOrDefault(x => x.Name == key);
                if (property == null)
                    if (throwOnConversionError)
                        throw new ArgumentException($"Property {key} does not exist");
                    else
                        continue;
                object convertedValue = null;
                try
                {
                    convertedValue = ObjectConverter.ToType(value, property.PropertyType);
                }
                catch (Exception e)
                {
                    convertedValue = null;
                    if (throwOnConversionError)
                        throw new Exception($"Error converting value {value} to type {property.PropertyType.Name}", e);
                }
                property.SetValue(instance, convertedValue);
            }
            return instance;
        }

        public T[] DeserializeSql(string data, bool throwOnConversionError = true)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!data.StartsWith("[") || !data.EndsWith("]"))
                throw new ArgumentException("Data is not a valid JSON array");
            data = data.TrimStart('[').TrimEnd(']');
            string[] values = data.Split(new []{"],["}, StringSplitOptions.RemoveEmptyEntries);
            List<T> instances = new List<T>();
            foreach (var stringArray in values)
            {
                instances.Add(DeserializeSqlInternal(stringArray, throwOnConversionError));
            }
            return instances.ToArray();
        }

        private T DeserializeSqlInternal(string data, bool throwOnConversionError = true)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            string[] values = data.Split(new []{","}, StringSplitOptions.RemoveEmptyEntries);
            if (values.Length != _properties.Length)
                throw new ArgumentException("Data does not match the number of properties");
            T instance = Activator.CreateInstance<T>();
            for (int i = 0; i < _properties.Length; i++)
            {
                var property = _properties[i];
                string value = values[i];
                if (value == "NULL")
                    continue;
                object convertedValue = null;
                try
                {
                    convertedValue = ObjectConverter.ToType(value, property.PropertyType);
                }
                catch (Exception e)
                {
                    convertedValue = null;
                    if (throwOnConversionError)
                        throw new Exception($"Error converting value {value} to type {property.PropertyType.Name}", e);
                }
                property.SetValue(instance, convertedValue);
            }

            return instance;
        }

        public IEnumerable<T> DeserializeEnumerable(string data, bool throwOnConversionError = true) => DeserializeEnumerableInternal(data, throwOnConversionError, new []{"},"});
        public IEnumerable<T> DeserializeEnumerableMessage(string data, bool throwOnConversionError = true) => DeserializeEnumerableInternal(data, throwOnConversionError, new []{"};"});
        private IEnumerable<T> DeserializeEnumerableInternal(string data, bool throwOnConversionError, string[] seperatorArray)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data = data.Replace(" ", "");
            if (!data.StartsWith("[") || !data.EndsWith("]"))
                throw new ArgumentException("Data is not a valid JSON array");
            string[] values = data.Substring(1, data.Length - 2).Split(seperatorArray, StringSplitOptions.RemoveEmptyEntries);
            List<T> instances = new List<T>();
            foreach (var jsonObject in values)
            {
                instances.Add(Deserialize(jsonObject, throwOnConversionError));
            }
            return instances;
        }

        public string Serialize(T data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (typeof(T).CanConvertToDbType())
            {
                return ObjectConverter.FromType(data);
            }
            string[] values = new string[_properties.Length];
            for (int i = 0; i < _properties.Length; i++)
            {
                var property = _properties[i];
                var value = property.GetValue(data);
                values[i] = property.Name + ":";
                values[i] += ObjectConverter.FromType(value);
            }
            return $"{{{string.Join(",", values)}}}";
        }

        public string Serialize(IEnumerable<T> data) => SerializeInternal(data, ",");
        public string SerializeForMessage(IEnumerable<T> data) => SerializeInternal(data, ";");
        private string SerializeInternal(IEnumerable<T> data, string separator)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data = data.ToArray();
            string[] values = new string[data.Count()];
            int i = 0;
            foreach (var item in data)
            {
                values[i] = Serialize(item);
                i++;
            }
            return $"[{string.Join(separator, values)}]";
        }

        public string SerializeSql(T data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            string[] values = new string[_properties.Length];
            for (int i = 0; i < _properties.Length; i++)
            {
                var property = _properties[i];
                var value = property.GetValue(data);
                values[i] += ObjectConverter.FromType(value);
            }
            return $"{string.Join(",", values)}";
        }

        public string SerializeSql(IEnumerable<T> data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            data = data.ToArray();
            var length = _properties.Length * data.Count();
            string[] values = new string[length];
            foreach (var obj in data)
            {
                for (int i = 0; i < _properties.Length; i++)
                {
                    var property = _properties[i];
                    var value = property.GetValue(obj);
                    values[i] += ObjectConverter.FromType(value);
                }
            }
            return $"{string.Join(",", values)}";
        }

        public string SerializeSqlWhere(T data) => SerializeSqlInternal(data, " AND ");
        public string SerializeSqlSet(T data) => SerializeSqlInternal(data, ", ");
        public string SerializeSqlInternal(T data, string seperator)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            string[] values = new string[_properties.Length];
            for (int i = 0; i < _properties.Length; i++)
            {
                var property = _properties[i];
                var value = property.GetValue(data);
                values[i] += $"{property.Name}={ObjectConverter.FromType(value)}";
            }
            return $"{string.Join(seperator, values)}";
        }

        public DBTypeConverter()
        {
            bool primaryKey = typeof(T).GetInterfaces().Contains(typeof(IIDEntity));
            var properties = typeof(T).GetProperties();
            List<PropertyInfo> propertyCache = new List<PropertyInfo>();
            foreach (var property in properties)
            {
                if (!property.CanWrite)
                    continue;
                if (!property.PropertyType.CanConvertToDbType())
                    continue;
                if (property.CustomAttributes.Any(x => x.AttributeType == typeof(DBIgnoreAttribute)))
                    continue;
                propertyCache.Add(property);
            }
            _properties = propertyCache.ToArray();

            foreach (var validProperty in _properties)
            {
                var sqlType = validProperty.PropertyType.ToDbType().ToSqlType().ToString().ToUpper();
                if (sqlType.Contains("VARCHAR") || sqlType.Contains("TEXT") || sqlType.Contains("BLOB"))
                    sqlType += "(255)";
                MySqlTypesString += $"{validProperty.Name} {sqlType}, ";
                if (primaryKey && validProperty.Name == "ID")
                    MySqlTypesString += "PRIMARY KEY(ID),";
            }
            MySqlTypesString = MySqlTypesString.Substring(0, MySqlTypesString.Length - 2);
        }

        public PropertyInfo[] TrackedProperties => _properties;
    }
}