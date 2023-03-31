namespace DBAPI.Library.Example
{
    using System;
    using System.Linq;
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
            apiclient.OnMessageReceived += (sender, message) =>
            {
                Console.WriteLine(message);
                try
                {
                    Console.WriteLine(converter.Deserialize(message).MyString);
                }
                catch (Exception e)
                {
                    Console.WriteLine(e);
                }
            };

            apiclient.Connect();
            apiclient.ListenToMessages("test");
            apiclient.SendMessage("test",new MyDataType()
            {
                MyString = "real",
            });
            apiclient.EnsureCollectionExists<MyDataType>("testcol");
            var myObject = new MyDataType()
            {
                ID = 1,
                MyString = "real2",
            };
            try
            {
                apiclient.Insert("testcol", myObject);
            }
            catch (ArgumentException e)
            {
                if (!e.Message.Contains("duplicate"))
                    throw;
            }
            //var entity = apiclient.GetById<MyDataType>("testcol", 1);
            var entity = apiclient.GetByFields<MyDataType>("testcol", ("MyString", "real2"), ("ID", 1)).FirstOrDefault();
            Console.WriteLine(entity.MyString);
            entity.MyString = "real3";
            apiclient.Update("testcol", entity);
            entity = apiclient.GetById<MyDataType>("testcol", 1);
            Console.WriteLine(entity.MyString);
            apiclient.Delete("testcol", myObject);
            apiclient.DeleteCollection("testcol");

            apiclient.Ping("test");
            var status = apiclient.GetServerStatus("test");
            Console.WriteLine((DateTime.UtcNow - status).TotalMilliseconds);
            Console.Read();

            apiclient.Dispose();
        }
    }
}