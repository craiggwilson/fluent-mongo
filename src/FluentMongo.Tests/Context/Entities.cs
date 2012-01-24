using System;
using System.Collections.Generic;

using MongoDB.Bson.Serialization.Attributes;
using MongoDB.Bson;

namespace FluentMongo.Context
{
    public class Person
    {
        public ObjectId Id { get; set; }

        [BsonElement("fn")]
        public string FirstName { get; set; }

        [BsonElement("ln")]
        public string LastName { get; set; }
    }
}
