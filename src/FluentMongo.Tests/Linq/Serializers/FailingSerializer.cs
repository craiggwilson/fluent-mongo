using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace FluentMongo.Linq.Serializers
{
    class FailingSerializer : BsonBaseSerializer
    {
        public override object Deserialize(BsonReader bsonReader, Type nominalType, Type actualType, IBsonSerializationOptions options)
        {
            throw new InvalidOperationException();
        }

        public override void Serialize(BsonWriter bsonWriter, Type nominalType, object value, IBsonSerializationOptions options)
        {
            throw new InvalidOperationException();
        }
    }
}
