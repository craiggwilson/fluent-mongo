using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization;
using FluentMongo.Linq.Serializers;

namespace FluentMongo.Linq.ClassMaps
{
    class CustomSerializedFieldClassMap : BsonClassMap<CustomSerializedField>
    {
        public CustomSerializedFieldClassMap()
        {
            base.AutoMap();
            base.MapField(f => f.StringSerializer).SetSerializer(new StringInt32Serializer());
            base.MapField(f => f.ThrowWhenSerializedAndDeserialized).SetSerializer(new FailingStringSerializer());
            base.MapField(f => f.ThrowWhenDeserialized).SetSerializer(new FailingStringSerializer(false, true));
        }
    }
}
