namespace DBAPI.Library.Example
{
    using System;
    using DBAPI.Library.Models;

    public class MyDataType : IIDEntity
    {
        public long ID { get; set; }
        public string MyString { get; set; }
        public MyEnum MyEnum { get; set; }
        public DateTime MyDateTime { get; set; }
    }

    public enum MyEnum
    {
        One = 1,
        Two,
        Three,
    }
}