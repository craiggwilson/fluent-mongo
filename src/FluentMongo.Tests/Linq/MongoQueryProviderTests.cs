using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
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
            Assert.AreEqual(new BsonDocument("add.IsInternational", true), queryObject.Query);
        }

        [Test]
        public void Boolean_Inverse()
        {
            var people = Collection.AsQueryable().Where(x => !x.PrimaryAddress.IsInternational);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(new BsonDocument("add.IsInternational", false), queryObject.Query);
        }

        [Test]
        public void Boolean_In_Conjunction()
        {
            var people = Collection.AsQueryable().Where(x => x.PrimaryAddress.IsInternational && x.Age > 21);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(new BsonDocument(
                                new BsonElement("add.IsInternational", true),
                                new BsonElement("age", new BsonDocument("$gt", 21))), queryObject.Query);
        }

        [Test]
        public void Chained()
        {
            var people = Collection.AsQueryable()
                .Select(x => new {Name = x.FirstName + x.LastName, x.Age })
                .Where(x => x.Age > 21)
                .Select(x => x.Name);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(3, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("age", new BsonDocument("$gt", 21)), queryObject.Query);
        }

        [Test]
        public void Chained2()
        {
            var people = Collection.AsQueryable()
                .Select(x => new { Name = x.FirstName + x.LastName, x.Age })
                .Where(x => x.Name == "BobMcBob")
                .Select(x => x.Name);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(3, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("$where", @"((this.fn + this.ln) === ""BobMcBob"")"), queryObject.Query);
        }

        [Test]
        public void ConjuctionConstraint()
        {
            var people = Collection.AsQueryable().Where(p => p.Age > 21 && p.Age < 42);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument(
                    "age",
                    new BsonDocument(
                        new BsonElement("$gt", 21),
                        new BsonElement("$lt", 42))),
                queryObject.Query);
        }

        [Test]
        public void ConstraintsAgainstLocalReferenceMember()
        {
            var local = new {Test = new {Age = 21}};
            var people = Collection.AsQueryable().Where(p => p.Age > local.Test.Age);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument(
                    "age",
                    new BsonDocument("$gt", local.Test.Age)),
                queryObject.Query);
        }

        [Test]
        public void ConstraintsAgainstLocalVariable()
        {
            var age = 21;
            var people = Collection.AsQueryable().Where(p => p.Age > age);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument(
                    "age",
                    new BsonDocument(
                        "$gt",
                        age)),
                queryObject.Query);
        }

        [Test]
        public void Disjunction()
        {
            var people = Collection.AsQueryable().Where(x => x.Age == 21 || x.Age == 35);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument(
                    "$or",
                    new BsonArray
                        {
                            new BsonDocument("age", 21),
                            new BsonDocument("age", 35)
                        }),
                queryObject.Query);
        }

        [Test]
        public void BsonDocumentQuery()
        {
            var people = from p in BsonDocumentCollection.AsQueryable()
                         where p.Key("age") > 21
                         select (string)p["fn"];

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(new BsonDocument { { "fn", 1 }, { "_id", 0 } }, queryObject.Fields);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument("age", new BsonDocument("$gt", 21)),
                queryObject.Query);
        }

        [Test]
        public void Enum()
        {
            var people = Collection.AsQueryable().Where(x => x.PrimaryAddress.AddressType == AddressType.Company);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("add.AddressType", (int) AddressType.Company), queryObject.Query);
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
            Assert.AreEqual(
                new BsonDocument(
                    "fn",
                    new BsonDocument(
                        "$in",
                        new BsonArray {"Jack", "Bob"})),
                queryObject.Query);
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
            Assert.AreEqual(
                new BsonDocument(
                    "fn",
                    new BsonDocument(
                        "$in",
                        new BsonArray {"Jack", "Bob"})),
                queryObject.Query);
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
            Assert.AreEqual(new BsonDocument("emps", new BsonDocument("$size", 1)), queryObject.Query);
        }

        [Test]
        public void NestedArray_indexer()
        {
            var people = Collection.AsQueryable().Where(x => x.EmployerIds[0] == 1);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("emps.0", 1), queryObject.Query);
        }

        [Test]
        public void NestedClassConstraint()
        {
            var people = Collection.AsQueryable().Where(p => p.PrimaryAddress.City == "my city");

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("add.city", "my city"), queryObject.Query);
        }

        [Test]
        public void NestedCollection_Count()
        {
            var people = from p in Collection.AsQueryable()
                         where p.Addresses.Length == 1
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("otherAdds", new BsonDocument("$size", 1)), queryObject.Query);
        }

        [Test]
        public void NestedList_Contains()
        {
            var people = Collection.AsQueryable().Where(x => x.Hobbies.Contains("soccer"));

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("Hobbies", "soccer"), queryObject.Query);
        }

        [Test]
        public void NestedList_Indexer()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses[1].City == "Tokyo");

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("otherAdds.1.city", "Tokyo"), queryObject.Query);
        }

        [Test]
        public void NestedQueryable_Any()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.Any(a => a.City == "London"));

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument(
                    "otherAdds",
                    new BsonDocument(
                        "$elemMatch",
                        new BsonDocument("city", "London"))),
                queryObject.Query);
        }

        [Test]
        public void NestedQueryable_Contains()
        {
            var people = Collection.AsQueryable().Where(x => x.EmployerIds.Contains(1));

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("emps", 1), queryObject.Query);
        }

        [Test]
        public void Nested_Queryable_Count()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.Count() == 1);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument(
                    "otherAdds",
                    new BsonDocument("$size", 1)),
                queryObject.Query);
        }

        [Test]
        public void Nested_Queryable_ElementAt()
        {
            var people = Collection.AsQueryable().Where(x => x.Addresses.ElementAt(1).City == "Tokyo");

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("otherAdds.1.city", "Tokyo"), queryObject.Query);
        }

        [Test]
        public void NotNullCheck()
        {
            var people = Collection.AsQueryable().Where(x => x.MidName != null);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument(
                    "MidName",
                    new BsonDocument("$ne", BsonNull.Value)),
                queryObject.Query);
        }

        [Test]
        public void NullCheck()
        {
            var people = Collection.AsQueryable().Where(x => x.MidName == null);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("MidName", BsonNull.Value), queryObject.Query);
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
            Assert.AreEqual(new BsonDocument("LinkedId", BsonNull.Value), queryObject.Query);
        }

        [Test]
        public void OrderBy()
        {
            var people = Collection.AsQueryable().OrderBy(x => x.Age).ThenByDescending(x => x.LastName);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument(
                    new BsonElement("age", 1),
                    new BsonElement("ln", -1)),
                queryObject.Sort);
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
            Assert.AreEqual(
                new BsonDocument(
                    new BsonElement("fn", 1),
                    new BsonElement("ln", 1),
                    new BsonElement("_id", 0)),
                queryObject.Fields);
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
            Assert.AreEqual(
                new BsonDocument(
                    new BsonElement("fn", 1),
                    new BsonElement("ln", 1),
                    new BsonElement("_id", 0)),
                queryObject.Fields);

            Assert.AreEqual(
                new BsonDocument(
                    "age",
                    new BsonDocument(
                        new BsonElement("$gt", 21),
                        new BsonElement("$lt", 42))),
                queryObject.Query);
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
            Assert.AreEqual(new BsonDocument("fn", new BsonRegularExpression("Joe")), queryObject.Query);
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
            Assert.AreEqual(new BsonDocument("fn", new BsonRegularExpression("Joe", "i")), queryObject.Query);
        }

        [Test]
        public void ReverseEqualConstraint()
        {
            var people = Collection.AsQueryable().Where(p => "Jack" == p.FirstName);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(new BsonDocument("fn", "Jack"), queryObject.Query);
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
        [TestCase("o")]
        [TestCase(@".$^{[(|)*+?\")]
        public void String_Contains(string value)
        {
            var people = from p in Collection.AsQueryable()
                         where p.FirstName.Contains(value)
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument("fn", new BsonRegularExpression(Regex.Escape(value))),
                queryObject.Query);
        }

        [Test]
        [TestCase("e")]
        [TestCase(@".$^{[(|)*+?\")]
        public void String_EndsWith(string value)
        {
            var people = from p in Collection.AsQueryable()
                         where p.FirstName.EndsWith(value)
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument("fn", new BsonRegularExpression(Regex.Escape(value) + "$")),
                queryObject.Query);
        }

        [Test]
        [TestCase("J")]
        [TestCase(@".$^{[(|)*+?\")]
        public void String_StartsWith(string value)
        {
            var people = from p in Collection.AsQueryable()
                         where p.FirstName.StartsWith(value)
                         select p;

            var queryObject = ((IMongoQueryable)people).GetQueryObject();
            Assert.AreEqual(0, queryObject.Fields.ElementCount);
            Assert.AreEqual(0, queryObject.NumberToLimit);
            Assert.AreEqual(0, queryObject.NumberToSkip);
            Assert.AreEqual(
                new BsonDocument("fn", new BsonRegularExpression("^" + Regex.Escape(value))),
                queryObject.Query);
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

        [Test]
        public void OfType()
        {
            var people = Collection.AsQueryable().OfType<Employee>();

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(new BsonDocument("_t", typeof(Employee).Name), queryObject.Query);
        }

        [Test]
        public void IsType()
        {
            var people = Collection.AsQueryable().Where(p => p is Employee);

            var queryObject = ((IMongoQueryable)people).GetQueryObject();

            Assert.AreEqual(new BsonDocument("_t", typeof(Employee).Name), queryObject.Query);
        }

        [Test]
        public void ConvertUsingRepresentation()
        {
            var id = ObjectId.GenerateNewId();
            var mapped = GetCollection<MappedField>("mapped").AsQueryable()
                .Where(m => m.Id == id.ToString() && m.CharAsInt32 == ' ');

            var queryObject = ((IMongoQueryable)mapped).GetQueryObject();

            Assert.AreEqual(new BsonDocument(new BsonElement("_id", id), new BsonElement("CharAsInt32", (int)' ')), queryObject.Query);
        }

        [Test]
        public void ConvertUsingCustomSerializer()
        {
            RegisterClassMapIfNecessary<ClassMaps.CustomSerializedFieldClassMap>();

            var mapped = GetCollection<CustomSerializedField>("serialized").AsQueryable()
                .Where(m => m.StringSerializer == "42");

            var queryObject = ((IMongoQueryable)mapped).GetQueryObject();

            Assert.AreEqual(new BsonDocument("StringSerializer", 42), queryObject.Query);
        }

        [Test]
        public void ThrowExceptionWhenProblemWithSerializer()
        {
            RegisterClassMapIfNecessary<ClassMaps.CustomSerializedFieldClassMap>();

            var mapped = GetCollection<CustomSerializedField>("serialized").AsQueryable()
                .Where(m => m.ThrowWhenSerializedAndDeserialized == "test");

            var ex = Assert.Throws<InvalidQueryException>(() => ((IMongoQueryable)mapped).GetQueryObject());
            Assert.IsInstanceOf<System.InvalidOperationException>(ex.InnerException);
        }

        [Test]
        public void ProjectionWithoutIdShouldUnselectItExplicitly()
        {
            var noId = GetCollection<NoIdEntity>("no_id").AsQueryable().Select(n => n.Name);

            var queryObject = ((IMongoQueryable)noId).GetQueryObject();

            Assert.AreEqual(
                new BsonDocument(
                    new BsonElement("Name", 1),
                    new BsonElement("_id", 0)),
                queryObject.Fields);
        }

        [Test]
        public void QueryWithCustomIdEntity()
        {
            var customId = GetCollection<CustomIdEntity>("custom_id").AsQueryable().Where(c => c.Id.FirstName == "firstname");
            var queryObject = ((IMongoQueryable)customId).GetQueryObject();

            Assert.AreEqual(
                new BsonDocument(new BsonElement("_id.FirstName", "firstname")),
                queryObject.Query);
        }

        [Test]
        public void SelectSubIdFieldsWithCustomIdEntity()
        {
            var customId = GetCollection<CustomIdEntity>("custom_id").AsQueryable().Select(c => new { c.Id.FirstName, c.Id.LastName });
            var queryObject = ((IMongoQueryable)customId).GetQueryObject();

            Assert.AreEqual(
                new BsonDocument(
                    new BsonElement("_id.FirstName", 1),
                    new BsonElement("_id.LastName", 1)),
                queryObject.Fields);
        }
    }
}