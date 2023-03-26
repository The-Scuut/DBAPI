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

        public T Deserialize(string data)
        {
            throw new NotImplementedException();
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