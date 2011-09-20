using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization.Serializers;
using MongoDB.Bson.IO;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;

namespace FluentMongo.Linq.Serializers
{
    class StringInt32Serializer : BsonBaseSerializer
    {
        public override object Deserialize(BsonReader bsonReader, Type nominalType, Type actualType, IBsonSerializationOptions options)
        {
            if (bsonReader.CurrentBsonType == BsonType.Int32)
                return bsonReader.ReadInt32().ToString();

            throw new InvalidOperationException();
        }

        public override void Serialize(BsonWriter bsonWriter, Type nominalType, object value, IBsonSerializationOptions options)
        {
            int intValue;
            if (value is string && int.TryParse((string)value, out intValue))
                bsonWriter.WriteInt32(intValue);
            else
                throw new InvalidOperationException();
        }
    }
}
