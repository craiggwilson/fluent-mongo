using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.DefaultSerializer;

namespace FluentMongo.Linq
{
    [TestFixture]
    public class SimpleTest
    {
        private class Employee
        {
            public ObjectId Id { get; set; }

            [BsonElement("firstName")]
            public string FirstName { get; set; }

            public string LastName { get; set; }
        }


        [Test]
        public void Test()
        {
            var employees = MongoDatabase.Create("mongodb://localhost/test")
                .GetCollection<Employee>("employees");

            var emps = from emp in employees.AsQueryable()
                       where emp.FirstName == "John"
                       select new { fn = emp.FirstName, ls = emp.LastName };

            var list = emps.ToList();
        }


    }
}