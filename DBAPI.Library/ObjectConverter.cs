namespace DBAPI.Library
{
    using System;
    using System.Collections.Generic;
    using System.Data;

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
            return ConvertTypeCodeToDbType(Type.GetTypeCode(type.UnderlyingSystemType));
        }

        public static SqlDbType ToSqlType(this DbType dbType, bool allowUnsigned = false)
        {
            switch (dbType)
            {
                case DbType.Object:
                    throw new ArgumentException("DbType.Object is not allowed", nameof(dbType));
                case DbType.Binary:
                    return SqlDbType.Binary;
                case DbType.Boolean:
                    return SqlDbType.Bit;
                case DbType.Byte:
                    return SqlDbType.TinyInt;
                case DbType.Currency:
                    return SqlDbType.Money;
                case DbType.Date:
                    return SqlDbType.Date;
                case DbType.Decimal:
                    return SqlDbType.Decimal;
                case DbType.Double:
                    return SqlDbType.Float;
                case DbType.Guid:
                    return SqlDbType.UniqueIdentifier;
                case DbType.Int16:
                    return SqlDbType.SmallInt;
                case DbType.Int32:
                    return SqlDbType.Int;
                case DbType.Int64:
                    return SqlDbType.BigInt;
                case DbType.Single:
                    return SqlDbType.Real;
                case DbType.String:
                    return SqlDbType.NVarChar;
                case DbType.Time:
                    return SqlDbType.Time;
                case DbType.Xml:
                    return SqlDbType.Xml;
                case DbType.AnsiString:
                    return SqlDbType.VarChar;
                case DbType.DateTime:
                    return SqlDbType.DateTime;
                case DbType.DateTime2:
                    return SqlDbType.DateTime2;
                case DbType.SByte:
                    return allowUnsigned ? SqlDbType.TinyInt : throw new ArgumentException("Unsigned types are not allowed", nameof(dbType));
                case DbType.UInt16:
                    return allowUnsigned ? SqlDbType.SmallInt : throw new ArgumentException("Unsigned types are not allowed", nameof(dbType));
                case DbType.UInt32:
                    return allowUnsigned ? SqlDbType.Int : throw new ArgumentException("Unsigned types are not allowed", nameof(dbType));
                case DbType.UInt64:
                    return allowUnsigned ? SqlDbType.BigInt : throw new ArgumentException("Unsigned types are not allowed", nameof(dbType));
                case DbType.VarNumeric:
                    return SqlDbType.Decimal;
                case DbType.DateTimeOffset:
                    return SqlDbType.DateTimeOffset;
                case DbType.StringFixedLength:
                    return SqlDbType.NChar;
                case DbType.AnsiStringFixedLength:
                    return SqlDbType.Char;
                default:
                    throw new ArgumentException("Unknown DbType", nameof(dbType));
            }
        }

        // minor amount of copy pasting of stuff that dosent exist in all target frameworks
        public static DbType ConvertTypeCodeToDbType(TypeCode typeCode)
        {
            switch (typeCode)
            {
                case TypeCode.Empty:
                case TypeCode.Object:
                case TypeCode.DBNull:
                    return DbType.Object;
                case TypeCode.Boolean:
                    return DbType.Boolean;
                case TypeCode.Char:
                    return DbType.StringFixedLength;
                case TypeCode.SByte:
                    return DbType.SByte;
                case TypeCode.Byte:
                    return DbType.Byte;
                case TypeCode.Int16:
                    return DbType.Int16;
                case TypeCode.UInt16:
                    return DbType.UInt16;
                case TypeCode.Int32:
                    return DbType.Int32;
                case TypeCode.UInt32:
                    return DbType.UInt32;
                case TypeCode.Int64:
                    return DbType.Int64;
                case TypeCode.UInt64:
                    return DbType.UInt64;
                case TypeCode.Single:
                    return DbType.Single;
                case TypeCode.Double:
                    return DbType.Double;
                case TypeCode.Decimal:
                    return DbType.Decimal;
                case TypeCode.DateTime:
                    return DbType.DateTime;
                case TypeCode.String:
                    return DbType.String;
                default:
                    return DbType.Object;
            }
        }
    }
}