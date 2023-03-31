namespace DBAPI.Library
{
    using System;
    using System.Collections.Generic;
    using System.Data;
    using System.Data.SqlClient;
    using System.Web.UI.WebControls;

    public static class ObjectConverter
    {
        public static Dictionary<Type, IDBTypeConverter> TypeConverters = new Dictionary<Type, IDBTypeConverter>();

        public static DBTypeConverter<T> GetTypeConverter<T>() where T : class
        {
            var type = typeof(T);
            if (TypeConverters.TryGetValue(type, out var converter))
                return converter as DBTypeConverter<T>;
            Type[] typeArgs = { type };
            var typeConverter = typeof(DBTypeConverter<>).MakeGenericType(typeArgs);
            object instance = Activator.CreateInstance(typeConverter);
            return instance as DBTypeConverter<T>;
        }

        public static IDBTypeConverter GetTypeConverter(Type type)
        {
            if (TypeConverters.TryGetValue(type, out var converter))
                return converter;
            Type[] typeArgs = { type };
            var typeConverter = typeof(DBTypeConverter<>).MakeGenericType(typeArgs);
            return (Activator.CreateInstance(typeConverter) as IDBTypeConverter)!;
        }

        public static bool CanConvertToDbType(this Type type) =>
            type.UnderlyingSystemType == type || type.UnderlyingSystemType == null;

        public static DbType ToDbType(this Type type, bool throwIfNotSystemType = true)
        {
            if (!type.CanConvertToDbType())
                return throwIfNotSystemType ? throw new ArgumentException("Type is not a system type") : DbType.Object;
            return Parameter.ConvertTypeCodeToDbType(Type.GetTypeCode(type.UnderlyingSystemType));
        }

        public static SqlDbType ToSqlType(this DbType type)
        {
            var sqlParam = new SqlParameter();
            sqlParam.DbType = type;
            return sqlParam.SqlDbType;
        }
    }
}