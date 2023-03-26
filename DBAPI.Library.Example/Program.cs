namespace DBAPI.Library.Example
{
    using System;

    internal class Program
    {
        public static void Main(string[] args)
        {
            var converter = ObjectConverter.GetTypeConverter<MyDataType>();
            Console.WriteLine(converter.MySqlTypesString);
        }
    }
}