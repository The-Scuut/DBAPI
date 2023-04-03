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
                MyEnum = MyEnum.Two,
                MyDateTime = DateTime.UtcNow,
            };
            var dateTime = DateTime.Now;
            var myObject2 = new MyDataType()
            {
                ID = 2,
                MyString = "obj3",
                MyEnum = MyEnum.One,
                MyDateTime = dateTime,
            };
            var collection = apiclient.GetCollection<MyDataType>("testcol");
            if (collection.Any(x => x.ID == myObject.ID))
                apiclient.DeleteById("testcol", myObject.ID);
            if (collection.Any(x => x.ID == myObject2.ID))
                apiclient.DeleteById("testcol", myObject2.ID);
            apiclient.Insert("testcol", myObject);
            apiclient.Insert("testcol", myObject2);
            var entity = apiclient.GetById<MyDataType>("testcol", 1);
            var entity2 = apiclient.GetByFields<MyDataType>("testcol", ("MyDateTime", $"{dateTime:yyyy-MM-dd HH:mm:ss}"), ("ID", 2))
                ?.FirstOrDefault();
            if (entity2 == null)
                throw new Exception("Entity2 is null");
            Console.WriteLine(entity.MyString);
            Console.WriteLine(entity2.MyEnum.ToString());
            entity.MyString = "real3";
            apiclient.Update("testcol", entity);
            entity = apiclient.GetById<MyDataType>("testcol", 1);
            Console.WriteLine(entity.MyString);
            apiclient.Delete("testcol", myObject);
            apiclient.Delete("testcol", myObject2);
            apiclient.DeleteCollection("testcol");

            apiclient.Ping("test");
            var status = apiclient.GetServerStatus("test");
            Console.WriteLine((DateTime.UtcNow - status).TotalMilliseconds);
            Console.Read();

            apiclient.Dispose();
        }
    }
}