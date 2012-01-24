using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using MongoDB.Bson;

namespace FluentMongo.Context.Tracking
{
    [TestFixture]
    public class UpdateDocumentBuilderTests
    {
        [Test]
        public void No_changes()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"" } }");
            var current = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"" } }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument(), updateDoc);
        }

        [Test]
        public void Simple_element_change()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"" }");
            var current = BsonDocument.Parse(@"{name: ""Jim"" }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$set", new BsonDocument("name", "Jim")),
                updateDoc);
        }

        [Test]
        public void Simple_element_type_change()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"" }");
            var current = BsonDocument.Parse(@"{name: 1 }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$set", new BsonDocument("name", 1)),
                updateDoc);
        }

        [Test]
        public void Simple_element_addition()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"" }");
            var current = BsonDocument.Parse(@"{name: ""Jack"", age: 42 }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$set", new BsonDocument("age", 42)),
                updateDoc);
        }

        [Test]
        public void Simple_element_removal()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", age: 42 }");
            var current = BsonDocument.Parse(@"{name: ""Jack""}");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$unset", new BsonDocument("age", 1)),
                updateDoc);
        }

        [Test]
        public void Simple_element_addition_and_removal_and_change()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", age: 42 }");
            var current = BsonDocument.Parse(@"{name: ""Jim"", birthdate: ""12/1/2001"" }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$unset", new BsonDocument("age", 1))
                .Add("$set", new BsonDocument().Add("birthdate", "12/1/2001").Add("name", "Jim")),
                updateDoc);
        }

        [Test]
        public void Complex_element_with_a_minor_change()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"" } }");
            var current = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Austin"" } }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$set", new BsonDocument("address.city", "Austin")),
                updateDoc);
        }

        [Test]
        public void Complex_element_with_an_addition()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"" } }");
            var current = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"", state: ""TX"" } }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$set", new BsonDocument("address.state", "TX")),
                updateDoc);
        }

        [Test]
        public void Complex_element_with_a_removal()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"" } }");
            var current = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."" } }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$unset", new BsonDocument("address.city", 1)),
                updateDoc);
        }

        [Test]
        public void Complex_element_with_a_wholesale_change()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"" } }");
            var current = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""234 Front Dr."", city: ""Denver"" } }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$set", new BsonDocument("address", BsonDocument.Parse(@"{ street: ""234 Front Dr."", city: ""Denver"" }"))),
                updateDoc);
        }

        [Test]
        public void Complex_element_with_a_wholesale_change_and_an_addition()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"" } }");
            var current = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""234 Front Dr."", city: ""Denver"", state: ""CO"" } }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$set", new BsonDocument("address", BsonDocument.Parse(@"{ street: ""234 Front Dr."", city: ""Denver"", state: ""CO"" }"))),
                updateDoc);
        }

        [Test]
        public void Complex_element_with_a_wholesale_change_and_a_removal()
        {
            var original = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""123 Main St."", city: ""Dallas"" } }");
            var current = BsonDocument.Parse(@"{name: ""Jack"", address: { street: ""234 Front Dr."" } }");

            var updateDoc = new UpdateDocumentBuilder(original, current).Build();

            Assert.AreEqual(new BsonDocument()
                .Add("$set", new BsonDocument("address", BsonDocument.Parse(@"{ street: ""234 Front Dr."" }"))),
                updateDoc);
        }
    }
}