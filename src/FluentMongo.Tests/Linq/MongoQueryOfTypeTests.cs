using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using MongoDB.Driver;

namespace FluentMongo.Linq
{
    [TestFixture]
    public class MongoQueryOfTypeTests : LinqTestBase
    {
        public override void SetupFixture()
        {
            base.SetupFixture();

            Collection.Insert(
                new Person
                {
                    FirstName = "Joe",
                    LastName = "McJoe",
                    Age = 21,
                    PrimaryAddress = new Address { City = "Chicago", IsInternational = true, AddressType = AddressType.Private },
                    Addresses = new[]
                    {
                        new Address { City = "Chicago", AddressType = AddressType.Private },
                        new Address { City = "London", AddressType = AddressType.Company }
                    },
                    EmployerIds = new[] { 3 }
                },
                SafeMode.True);

            Collection.Insert(new Employee
            {
                FirstName = "Jinny",
                LastName = "McJinny",
                Salary = 10000.0,
                Age = 42
            }, SafeMode.True);

            Collection.Insert(new Employee
            {
                FirstName = "Sonny",
                LastName = "McSonny",
                Salary = 50000.0,
                Age = 42
            }, SafeMode.True);
        }

        [Test]
        public void OfType()
        {
            var people = Collection.AsQueryable().OfType<Employee>().ToList();

            Assert.AreEqual(2, people.Count);
            Assert.AreEqual(1, people.Count(e => e.Salary == 10000.0));
            Assert.AreEqual(1, people.Count(e => e.Salary == 50000.0));
        }

        [Test]
        public void OfTypeAndWhere()
        {
            var people = Collection.AsQueryable().OfType<Employee>().Where(e => e.Salary > 20000.0).ToList();

            Assert.AreEqual(1, people.Count);
            Assert.AreEqual("Sonny", people.First().FirstName);
        }

        [Test]
        public void WhereThenOfTypeAndWhere()
        {
            var people = Collection.AsQueryable().Where(p => p.Age == 42).OfType<Employee>().Where(p => p.Salary > 20000.0).ToList();

            Assert.AreEqual(1, people.Count);
            Assert.AreEqual("Sonny", people.First().FirstName);
        }
    }
}
