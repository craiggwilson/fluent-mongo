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
                StringSerializer = "42",
                ThrowWhenDeserialized = "test"
            });
        }

        [Test]
        public void UseConverterInProjection()
        {
            var mapped = MappedCollection.AsQueryable().FirstOrDefault();

            Assert.NotNull(mapped.Id);
            Assert.AreEqual(' ', mapped.CharAsInt32);
        }

        [Test]
        public void UseFailingSerializerInProjection()
        {
            var serialized = CustomCollection.AsQueryable().Select(s => s.ThrowWhenDeserialized);

            Assert.Throws<InvalidOperationException>(() => serialized.First());
        }
    }
}
