namespace DBAPI.Library.Example
{
    using DBAPI.Library.Models;

    public class MyDataType : IIDEntity
    {
        public string MyString { get; set; }
        public long ID { get; set; }
    }
}