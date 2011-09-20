using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using MongoDB.Driver;

namespace FluentMongo.Linq
{
    [TestFixture]
    public class MongoQueryCustomSerializedTests : LinqTestBase
    {
        protected MongoCollection<MappedField> MappedCollection;
        protected MongoCollection<CustomSerializedField> CustomCollection;

        public override void SetupFixture()
        {
            base.SetupFixture();

            RegisterClassMapIfNecessary<ClassMaps.CustomSerializedFieldClassMap>();

            MappedCollection = GetCollection<MappedField>("mapped");

            MappedCollection.Insert(new MappedField { CharAsInt32 = ' ' });

            CustomCollection = GetCollection<CustomSerializedField>("serialized");

            CustomCollection.Insert(new CustomSerializedField
            {
                StringSerializer = "42"
            });

            CustomCollection.Insert(new CustomSerializedField
            {
                StringSerializer = "41",
                ThrowWhenDeserialized = "test"
            });
        }

        [Test]
        public void UseConverterInFullEntityProjection()
        {
            var mapped = MappedCollection.AsQueryable().First();

            Assert.NotNull(mapped.Id);
            Assert.AreEqual(' ', mapped.CharAsInt32);
        }

        [Test]
        public void UseConverterInPartialEntityProjection()
        {
            var mapped = MappedCollection.AsQueryable().Select(m => m.CharAsInt32).First();

            Assert.AreEqual(' ', mapped);
        }

        [Test]
        public void UseCustomSerializerInPartialEntityProjection()
        {
            var serialized = CustomCollection.AsQueryable().Select(s => s.StringSerializer).First();

            Assert.AreEqual("42", serialized);
        }

        [Test]
        public void UseCustomSerializerInArraySelectionArray()
        {
            var serialized = CustomCollection.AsQueryable().Where(s => new[] { "42", "43" }.Contains(s.StringSerializer)).Single();

            Assert.AreEqual("42", serialized.StringSerializer);
        }

        [Test]
        public void UseCustomSerializerInArraySelectionList()
        {
            var serialized = CustomCollection.AsQueryable().Where(s => new List<string>(new[] { "42", "43" }).Contains(s.StringSerializer)).Single();

            Assert.AreEqual("42", serialized.StringSerializer);
        }

        [Test]
        public void UseCustomSerializerInMultipleComparison()
        {
            var serialized = CustomCollection.AsQueryable().Where(s => s.StringSerializer == "42" || s.StringSerializer == "43").Single();

            Assert.AreEqual("42", serialized.StringSerializer);
        }

        [Test]
        public void UseFailingSerializerInProjection()
        {
            var serialized = CustomCollection.AsQueryable().Where(c => c.ThrowWhenDeserialized != null).Select(s => s.ThrowWhenDeserialized);

            var ex = Assert.Throws<System.Reflection.TargetInvocationException>(() => serialized.First());
            Assert.IsInstanceOf<InvalidOperationException>(ex.InnerException);
        }
    }
}
