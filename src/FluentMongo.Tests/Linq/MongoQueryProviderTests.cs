using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using System.Collections;
using MongoDB.Bson;

namespace FluentMongo.Linq
{
    [TestFixture]
    public class MongoQueryProviderTests : LinqTestBase
    {
        [Test]
        public void Boolean1()
        {
            var people = Collection.AsQueryable().Where(x => x.PrimaryAddress.IsInternational);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(@"{ ""add.IsInternational"" : true }", queryObject.Query.ToJson());
        }

        [Test]
        public void Boolean_Inverse()
        {
            var people = Collection.AsQueryable().Where(x => !x.PrimaryAddress.IsInternational);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(@"{ ""$not"" : { ""add.IsInternational"" : true } }", queryObject.Query.ToJson());
        }

        [Test]
        public void Boolean_In_Conjunction()
        {
            var people = Collection.AsQueryable().Where(x => x.PrimaryAddress.IsInternational && x.Age > 21);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(@"{ ""add.IsInternational"" : true, ""age"" : { ""$gt"" : 21 } }", queryObject.Query.ToJson());
        }

        [Test]
        public void Chained()
        {
            var people = Collection.AsQueryable()
                .Select(x => new {Name = x.FirstName + x.LastName, x.Age })
                .Where(x => x.Age > 21)
                .Select(x => x.Name);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(2, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""age"" : { ""$gt"" : 21 } }", queryObject.Query.ToJson());
        }

        [Test]
        public void Chained2()
        {
            var people = Collection.AsQueryable()
                .Select(x => new { Name = x.FirstName + x.LastName, x.Age })
                .Where(x => x.Name == "BobMcBob")
                .Select(x => x.Name);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(2, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""$where"" : ""((this.fn + this.ln) === \""BobMcBob\"")"" }", queryObject.Query.ToJson());
        }

        [Test]
        public void ConjuctionConstraint()
        {
            var people = Collection.AsQueryable().Where(p => p.Age > 21 && p.Age < 42);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""age"" : { ""$gt"" : 21, ""$lt"" : 42 } }", queryObject.Query.ToJson());
        }

        [Test]
        public void ConstraintsAgainstLocalReferenceMember()
        {
            var local = new {Test = new {Age = 21}};
            var people = Collection.AsQueryable().Where(p => p.Age > local.Test.Age);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""age"" : { ""$gt"" : " + local.Test.Age.ToString() + " } }", queryObject.Query.ToJson());
        }

        [Test]
        public void ConstraintsAgainstLocalVariable()
        {
            var age = 21;
            var people = Collection.AsQueryable().Where(p => p.Age > age);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""age"" : { ""$gt"" : " + age.ToString() + " } }", queryObject.Query.ToJson());
        }

        [Test]
        public void Disjunction()
        {
            var people = Collection.AsQueryable().Where(x => x.Age == 21 || x.Age == 35);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);

            Assert.AreEqual(@"{ ""$or"" : [{ ""age"" : 21 }, { ""age"" : 35 }] }", queryObject.Query.ToJson());
        }

        [Test]
        public void BsonDocumentQuery()
        {
            var people = from p in BsonDocumentCollection.AsQueryable()
                         where p.Key("age") > 21
                         select (string)p["fn"];

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(new BsonDocument { { "fn", 1 } }, queryObject.Fields);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""age"" : { ""$gt"" : 21 } }", queryObject.Query.ToJson());
        }

        [Test]
        public void Enum()
        {
            var people = Collection.AsQueryable().Where(x => x.PrimaryAddress.AddressType == AddressType.Company);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""add.AddressType"" : " + ((int)AddressType.Company).ToString() + " }", queryObject.Query.ToJson());
        }

        [Test]
        public void LocalEnumerable_Contains()
        {
            var names = new[] {"Jack", "Bob"};
            var people = Collection.AsQueryable().Where(x => names.Contains(x.FirstName));

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : { ""$in"" : [""Jack"", ""Bob""] } }", queryObject.Query.ToJson());
        }

        [Test]
        public void LocalList_Contains()
        {
            var names = new List<string> {"Jack", "Bob"};
            var people = Collection.AsQueryable().Where(x => names.Contains(x.FirstName));

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : { ""$in"" : [""Jack"", ""Bob""] } }", queryObject.Query.ToJson());
        }

        [Test]
        public void NestedArray_Length()
        {
            var people = from p in Collection.AsQueryable()
                         where p.EmployerIds.Length == 1
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""emps"" : { ""$size"" : 1 } }", queryObject.Query.ToJson());
        }

        [Test]
        public void NestedArray_indexer()
        {
            var people = Collection.AsQueryable().Where(x => x.EmployerIds[0] == 1);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""emps.0"" : 1 }", queryObject.Query.ToJson());
        }

        [Test]
        public void NestedClassConstraint()
        {
            var people = Collection.AsQueryable().Where(p => p.PrimaryAddress.City == "my city");

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""add.city"" : ""my city"" }", queryObject.Query.ToJson());
        }

        //[Test]
        //public void NestedCollection_Count()
        //{
        //    var people = from p in Collection.AsQueryable()
        //                 where p.Addresses.Length == 1
        //                 select p;

        //    var queryObject = ((IMongoQueryable)people).GetQueryObject();
        //    Assert.AreEqual(0, queryObject.Fields.ElementCount);
        //    Assert.AreEqual(0, queryObject.NumberToLimit);
        //    Assert.AreEqual(0, queryObject.NumberToSkip);
        //    Assert.AreEqual(@"{ ""otherAdds"" : { ""$size"" : 1 } }", queryObject.Query.ToJson());
        //}

        [Test]
        public void NestedList_indexer()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses[1].City == "Tokyo");

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""otherAdds.1.city"" : ""Tokyo"" }", queryObject.Query.ToJson());
        }

        [Test]
        public void NestedQueryable_Any()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.Any(a => a.City == "London"));

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""otherAdds"" : { ""$elemMatch"" : { ""city"" : ""London"" } } }", queryObject.Query.ToJson());
        }

        [Test]
        public void NestedQueryable_Contains()
        {
            var people = Collection.AsQueryable().Where(x => x.EmployerIds.Contains(1));

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""emps"" : 1 }" , queryObject.Query.ToJson());
        }

        [Test]
        public void Nested_Queryable_Count()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.Count() == 1);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""otherAdds"" : { ""$size"" : 1 } }", queryObject.Query.ToJson());
        }

        [Test]
        public void Nested_Queryable_ElementAt()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.ElementAt(1).City == "Tokyo");

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""otherAdds.1.city"" : ""Tokyo"" }", queryObject.Query.ToJson());
        }

        [Test]
        public void NotNullCheck()
        {
            var people = Collection.AsQueryable().Where(x => x.MidName != null);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""MidName"" : { ""$ne"" : null } }", queryObject.Query.ToJson());
        }

        [Test]
        public void NullCheck()
        {
            var people = Collection.AsQueryable().Where(x => x.MidName == null);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""MidName"" : null }", queryObject.Query.ToJson());
        }

        [Test]
        public void NullCheckOnClassTypes()
        {
            //BUG: this a bug related to id generation...
            var people = Collection.AsQueryable().Where(x => x.LinkedId == null);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""LinkedId"" : null }", queryObject.Query.ToJson());
        }

        [Test]
        public void OrderBy()
        {
            var people = Collection.AsQueryable().OrderBy(x => x.Age).ThenByDescending(x => x.LastName);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""age"" : 1, ""ln"" : -1 }", queryObject.Sort.ToJson());
        }

        [Test]
        public void Projection()
        {
            var people = from p in Collection.AsQueryable()
                         select new {Name = p.FirstName + p.LastName};

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(0, queryObject.Query.ElementCount);
            Assert.AreEqual(@"{ ""fn"" : 1, ""ln"" : 1 }", queryObject.Fields.ToJson());
        }

        [Test]
        public void ProjectionWithConstraints()
        {
            var people = from p in Collection.AsQueryable()
                         where p.Age > 21 && p.Age < 42
                         select new {Name = p.FirstName + p.LastName};

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : 1, ""ln"" : 1 }", queryObject.Fields.ToJson());
            Assert.AreEqual(@"{ ""age"" : { ""$gt"" : 21, ""$lt"" : 42 } }", queryObject.Query.ToJson());
        }

        [Test]
        public void ProjectionWithLocalCreation_ChildobjectShouldNotBeNull()
        {
            var people = Collection.AsQueryable()
                .Select(p => new PersonWrapper(p, p.FirstName));

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(0, queryObject.Query.ElementCount);
        }

        [Test]
        public void Regex_IsMatch()
        {
            var people = from p in Collection.AsQueryable()
                         where Regex.IsMatch(p.FirstName, "Joe")
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : { ""$regex"" : ""Joe"", ""$options"" : """" } }", queryObject.Query.ToJson());
        }

        [Test]
        public void Regex_IsMatch_CaseInsensitive()
        {
            var people = from p in Collection.AsQueryable()
                         where Regex.IsMatch(p.FirstName, "Joe", RegexOptions.IgnoreCase)
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : { ""$regex"" : ""Joe"", ""$options"" : ""i"" } }", queryObject.Query.ToJson());
        }

        [Test]
        public void ReverseEqualConstraint()
        {
            var people = Collection.AsQueryable().Where(p => "Jack" == p.FirstName);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : ""Jack"" }", queryObject.Query.ToJson());
        }

        [Test]
        public void SkipAndTake()
        {
            var people = Collection.AsQueryable().Skip(2).Take(1);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(1, queryObject.NumberToLimit);
            Assert.AreEqual(2, queryObject.NumberToSkip);
        }

        [Test]
        public void String_Contains()
        {
            var people = from p in Collection.AsQueryable()
                         where p.FirstName.Contains("o")
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : { ""$regex"" : ""o"", ""$options"" : """" } }", queryObject.Query.ToJson());
        }

        [Test]
        public void String_EndsWith()
        {
            var people = from p in Collection.AsQueryable()
                         where p.FirstName.EndsWith("e")
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : { ""$regex"" : ""e$"", ""$options"" : """" } }", queryObject.Query.ToJson());
        }

        [Test]
        public void String_StartsWith()
        {
            var people = from p in Collection.AsQueryable()
                         where p.FirstName.StartsWith("J")
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(@"{ ""fn"" : { ""$regex"" : ""^J"", ""$options"" : """" } }", queryObject.Query.ToJson());
        }

        [Test]
        public void WithoutConstraints()
        {
            var people = Collection.AsQueryable();

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(0, queryObject.Query.ElementCount);
        }
    }
}