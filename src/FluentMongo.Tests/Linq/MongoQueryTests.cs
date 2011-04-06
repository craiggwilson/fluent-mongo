using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

using MongoDB.Driver;

namespace FluentMongo.Linq
{
    [TestFixture]
    public class MongoQueryTests : LinqTestBase
    {
        private readonly Guid _searchableGuid = Guid.NewGuid();

        public override void SetupFixture()
        {
            base.SetupFixture();

            Collection.Insert(
                new Person
                {
                    FirstName = "Bob",
                    MidName = "Bart",
                    LastName = "McBob",
                    Age = 42,
                    PrimaryAddress = new Address {City = "London", IsInternational = true, AddressType = AddressType.Company},
                    Addresses = new[]
                    {
                        new Address { City = "London", IsInternational = true, AddressType = AddressType.Company },
                        new Address { City = "Tokyo", IsInternational = true, AddressType = AddressType.Private }, 
                        new Address { City = "Seattle", IsInternational = false, AddressType = AddressType.Private },
                        new Address { City = "Paris", IsInternational = true, AddressType = AddressType.Private } 
                    },
                    EmployerIds = new[] { 1, 2 },
                    Hobbies = new List<string> { "soccer", "tv" },
                    RefId = _searchableGuid
                }, SafeMode.True);

            Collection.Insert(
                new Person
                {
                    FirstName = "Jane",
                    LastName = "McJane",
                    Age = 35,
                    PrimaryAddress = new Address { City = "Paris", IsInternational = false, AddressType = AddressType.Private },
                    Addresses = new[]
                    {
                        new Address { City = "Paris", AddressType = AddressType.Private }
                    },
                    EmployerIds = new[] {1},
                    Hobbies = new List<string> { "soccer", "awesome" },
                },
                SafeMode.True);

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
                    EmployerIds = new[] {3}
                },
                SafeMode.True);
        }

        [Test]
        public void Any()
        {
            var anyone = Collection.AsQueryable().Any(x => x.Age <= 21);

            Assert.IsTrue(anyone);
        }

        [Test]
        public void Boolean()
        {
            var people = Enumerable.ToList(Collection.AsQueryable().Where(x => x.PrimaryAddress.IsInternational));

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        [Ignore("Mongodb does not support this type of query.")]
        public void Boolean_Inverse()
        {
            var people = Enumerable.ToList(Collection.AsQueryable().Where(x => !x.PrimaryAddress.IsInternational));

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void Boolean_In_Conjunction()
        {
            var people = Enumerable.ToList(Collection.AsQueryable().Where(x => x.PrimaryAddress.IsInternational && x.Age > 21));

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void Chained()
        {
            var people = Collection.AsQueryable()
                .Select(x => new { Name = x.FirstName + x.LastName, x.Age })
                .Where(x => x.Age > 21)
                .QueryDump(Log)
                .Select(x => x.Name).ToList();

            Assert.AreEqual(2, people.Count);
        }

        
        [Test]
        public void Chained2()
        {
            var people = Collection.AsQueryable()
                .Select(x => new { Name = x.FirstName + x.LastName, x.Age })
                .Where(x => x.Name == "BobMcBob")
                .QueryDump(Log)
                .Select(x => x.Name)
                .ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void Complex_Addition()
        {
            var people = Collection.AsQueryable().Where(x => x.Age + 23 < 50).ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void Complex_Disjunction()
        {
            var people = Collection.AsQueryable().Where(x => x.Age == 21 || x.Age == 35).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void ConjuctionConstraint()
        {
            var people = Collection.AsQueryable().Where(p => p.Age > 21 && p.Age < 42).ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void ConstraintsAgainstLocalReferenceMember()
        {
            var local = new { Test = new { Age = 21 } };
            var people = Collection.AsQueryable().Where(p => p.Age > local.Test.Age).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void ConstraintsAgainstLocalVariable()
        {
            var age = 21;
            var people = Collection.AsQueryable().Where(p => p.Age > age).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void Count()
        {
            var count = Collection.AsQueryable().Count();

            Assert.AreEqual(3, count);
        }

        [Test]
        public void Count_with_predicate()
        {
            var count = Collection.AsQueryable().Count(x => x.Age > 21);

            Assert.AreEqual(2, count);
        }

        [Test]
        public void Count_without_predicate()
        {
            var count = Collection.AsQueryable().Where(x => x.Age > 21).Count();

            Assert.AreEqual(2, count);
        }

        [Test]
        public void Disjunction()
        {
            var people = Collection.AsQueryable().Where(x => x.Age == 21 || x.Age == 35).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void DocumentQuery()
        {
            var people = (from p in BsonDocumentCollection.AsQueryable()
                          where p.Key("age") > 21
                          select (string)p["fn"]).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void Enum()
        {
            var people = Collection.AsQueryable()
                .Where(x => x.PrimaryAddress.AddressType == AddressType.Company)
                .ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void First()
        {
            var person = Collection.AsQueryable().OrderBy(x => x.Age).First();

            Assert.AreEqual("Joe", person.FirstName);
        }

        [Test]
        public void LocalArray_String_Contains()
        {
            var names = new[] { "Joe", "Bob" };
            var people = Collection.AsQueryable().Where(x => names.Contains(x.FirstName)).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void LocalEnumerable_Guid_Contains()
        {
            var ids = new List<Guid> { _searchableGuid }.AsEnumerable();
            var people = Collection.AsQueryable().Where(x => ids.Contains(x.RefId)).ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void LocalList_Contains()
        {
            var names = new List<string> { "Joe", "Bob" };
            var people = Collection.AsQueryable().Where(x => names.Contains(x.FirstName)).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void NestedArray_Length()
        {
            var people = (from p in Collection.AsQueryable()
                          where p.EmployerIds.Length == 1
                          select p).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void NestedArray_indexer()
        {
            var people = Collection.AsQueryable().Where(x => x.EmployerIds[0] == 1).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void NestedClassConstraint()
        {
            var people = Collection.AsQueryable().Where(p => p.PrimaryAddress.City == "London").ToList();

            Assert.AreEqual(1, people.Count);
        }

        //[Test]
        //public void NestedCollection_Count()
        //{
        //    //BUG: driver doesn't persist a generic list
        //    var people = (from p in Collection.AsQueryable()
        //                  where p.Addresses.Count == 1
        //                  select p).ToList();

        //    Assert.AreEqual(1, people.Count);
        //}

        [Test]
        public void NestedList_Contains()
        {
            var people = Collection.AsQueryable().Where(x => x.Hobbies.Contains("soccer")).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void NestedList_Indexer()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses[1].City == "Tokyo").ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void NestedQueryable_Any()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.Any(a => a.City == "Paris")).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void NestedQueryable_Contains()
        {
            var people = Collection.AsQueryable().Where(x => x.EmployerIds.Contains(1)).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void Nested_Queryable_Count()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.Count() == 1).ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void Nested_Queryable_ElementAt()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.ElementAt(1).City == "Tokyo").ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void NotNullCheck()
        {
            var people = Collection.AsQueryable().Where(x => x.MidName != null).ToArray();

            Assert.AreEqual(1, people.Length);
        }

        [Test]
        public void NullCheck()
        {
            var people = Collection.AsQueryable().Where(x => x.MidName == null).ToArray();

            Assert.AreEqual(2, people.Length);
        }

        [Test]
        public void NullCheckOnClassTypes()
        {
            var people = Collection.AsQueryable().Where(x => x.LinkedId == null).ToArray();

            Assert.AreEqual(3, people.Length);
        }

        [Test]
        public void OrderBy()
        {
            var people = Collection.AsQueryable().OrderBy(x => x.Age).ThenByDescending(x => x.LastName).ToList();

            Assert.AreEqual("Joe", people[0].FirstName);
            Assert.AreEqual("Jane", people[1].FirstName);
            Assert.AreEqual("Bob", people[2].FirstName);
        }

        [Test]
        public void Projection()
        {
            var people = (from p in Collection.AsQueryable()
                          select new { Name = p.FirstName + p.LastName }).ToList();

            Assert.AreEqual(3, people.Count);
        }

        [Test]
        public void ProjectionWithLocalCreation_ChildobjectShouldNotBeNull()
        {
            var people = Collection.AsQueryable()
                .Select(p => new PersonWrapper(p, p.FirstName))
                .FirstOrDefault();

            Assert.IsNotNull(people);
            Assert.IsNotNull(people.Name);
            Assert.IsNotNull(people.Person);
            Assert.IsNotNull(people.Person.PrimaryAddress);
        }

        [Test]
        public void ProjectionWithConstraints()
        {
            var people = (from p in Collection.AsQueryable()
                          where p.Age > 21 && p.Age < 42
                          select new { Name = p.FirstName + p.LastName }).ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void Regex_IsMatch()
        {
            var people = (from p in Collection.AsQueryable()
                          where Regex.IsMatch(p.FirstName, "Joe")
                          select p).ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void Regex_IsMatch_CaseInsensitive()
        {
            var people = (from p in Collection.AsQueryable()
                          where Regex.IsMatch(p.FirstName, "joe", RegexOptions.IgnoreCase)
                          select p).ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void ReverseEqualConstraint()
        {
            var people = Collection.AsQueryable().Where(p => "Joe" == p.FirstName).ToList();

            Assert.AreEqual(1, people.Count);
        }

        [Test]
        public void Single()
        {
            var person = Collection.AsQueryable().Where(x => x.Age == 21).Single();

            Assert.AreEqual("Joe", person.FirstName);
        }

        [Test]
        public void SkipAndTake()
        {
            var people = Collection.AsQueryable().OrderBy(x => x.Age).Skip(2).Take(1).ToList();

            Assert.AreEqual("Bob", people[0].FirstName);
        }

        [Test]
        public void String_Contains()
        {
            var people = (from p in Collection.AsQueryable()
                          where p.FirstName.Contains("o")
                          select p).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void String_EndsWith()
        {
            var people = (from p in Collection.AsQueryable()
                          where p.FirstName.EndsWith("e")
                          select p).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void String_StartsWith()
        {
            var people = (from p in Collection.AsQueryable()
                          where p.FirstName.StartsWith("J")
                          select p).ToList();

            Assert.AreEqual(2, people.Count);
        }

        [Test]
        public void WithoutConstraints()
        {
            var people = Collection.AsQueryable().ToList();

            Assert.AreEqual(3, people.Count);
        }

        public Action<string> log { get; set; }
    }
}