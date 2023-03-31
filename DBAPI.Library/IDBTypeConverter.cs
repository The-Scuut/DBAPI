namespace DBAPI.Library
{
    using System.Reflection;

    public interface IDBTypeConverter
    {
        public PropertyInfo[] TrackedProperties { get; }
    }
}