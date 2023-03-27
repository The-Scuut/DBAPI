namespace DBAPI.Library
{
    using System;
    using System.Collections.Generic;
    using System.Linq;
    using System.Reflection;

    public class DBTypeConverter<T> : IDBTypeConverter where T : class
    {
        private readonly PropertyInfo[] _properties;
        public readonly string MySqlTypesString = "";

        public T Deserialize(string data, bool throwOnConversionError = true)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!data.StartsWith("[") || !data.EndsWith("]"))
                throw new ArgumentException("Data is not a valid JSON array");
            string[] values = data.Substring(1, data.Length - 2).Split(',');
            if (values.Length != _properties.Length)
                throw new ArgumentException("Data does not match the number of properties");
            var instance = Activator.CreateInstance<T>();
            for (int i = 0; i < values.Length; i++)
            {
                var property = _properties[i];
                var value = values[i];
                if (value == "null")
                    continue;
                object convertedValue = null;
                try
                {
                    convertedValue = property.PropertyType.IsEnum
                        ? Enum.Parse(property.PropertyType, value)
                        : Convert.ChangeType(value, property.PropertyType);
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

        public IEnumerable<T> DeserializeEnumerable(string data, bool throwOnConversionError = true)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            if (!data.StartsWith("[") || !data.EndsWith("]"))
                throw new ArgumentException("Data is not a valid JSON array");
            string[] values = data.Substring(1, data.Length - 2).Split(',');
            if (values.Length % _properties.Length != 0)
                throw new ArgumentException("Data does not match the number of properties");
            List<T> instances = new List<T>();
            for (int i = 0; i < values.Length; i += _properties.Length)
            {
                var instance = Activator.CreateInstance<T>();
                for (int j = 0; j < _properties.Length; j++)
                {
                    var property = _properties[j];
                    var value = values[i + j];
                    if (value == "null")
                        continue;
                    object convertedValue = null;
                    try
                    {
                        convertedValue = property.PropertyType.IsEnum
                            ? Enum.Parse(property.PropertyType, value)
                            : Convert.ChangeType(value, property.PropertyType);
                    }
                    catch (Exception e)
                    {
                        convertedValue = null;
                        if (throwOnConversionError)
                            throw new Exception($"Error converting value {value} to type {property.PropertyType.Name}", e);
                    }
                    property.SetValue(instance, convertedValue);
                }
                instances.Add(instance);
            }
            return instances;
        }

        public string Serialize(T data)
        {
            if (data == null)
                throw new ArgumentNullException(nameof(data));
            string[] values = new string[_properties.Length];
            for (int i = 0; i < _properties.Length; i++)
            {
                var property = _properties[i];
                var value = property.GetValue(data);
                if (value == null)
                    values[i] = "null";
                else
                    values[i] = value.ToString();
            }
            return $"[{string.Join(",", values)}]";
        }

        public string Serialize(IEnumerable<T> data)
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
            return $"[{string.Join(",", values)}]";
        }

        public DBTypeConverter()
        {
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
            }
        }
    }
}