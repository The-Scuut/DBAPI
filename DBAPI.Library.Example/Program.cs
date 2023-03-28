namespace DBAPI.Library.Example
{
    using System;
    using DBAPI.Library.Models;

    internal class Program
    {
        public static void Main(string[] args)
        {
            var converter = ObjectConverter.GetTypeConverter<MyDataType>();
            Console.WriteLine(converter.MySqlTypesString);
            var apiclient = new APIClient(new APIClientParameters()
            {
                Host = "localhost",
                Token = "null",
            });

            apiclient.Connect();

            apiclient.Dispose();
        }
    }
}