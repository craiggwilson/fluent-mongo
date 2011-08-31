using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;

namespace FluentMongo.Linq.Serializers
{
    class FailingStringSerializer : BsonBaseSerializer
    {
        readonly bool _failOnSerialize;
        readonly bool _failOnDeserialize;

        public FailingStringSerializer() : this(true, true) { }
        public FailingStringSerializer(bool failOnSerialize, bool failOnDeserialize)
        {
            _failOnSerialize = failOnSerialize;
            _failOnDeserialize = failOnDeserialize;
        }

        public override object Deserialize(BsonReader bsonReader, Type nominalType, Type actualType, IBsonSerializationOptions options)
        {
            if (_failOnDeserialize)
                throw new InvalidOperationException();

            return bsonReader.ReadString();
        }

        public override void Serialize(BsonWriter bsonWriter, Type nominalType, object value, IBsonSerializationOptions options)
        {
            // when entry is null, do not throw, just write null
            if (value == null)
            {
                bsonWriter.WriteNull();
                return;
            }

            if (_failOnSerialize)
                throw new InvalidOperationException();

            bsonWriter.WriteString((string)value);
        }
    }
}
