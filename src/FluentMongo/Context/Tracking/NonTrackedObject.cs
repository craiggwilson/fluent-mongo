using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FluentMongo.Context.Tracking
{
    internal class NonTrackedObject : ITrackedObject
    {
        public BsonClassMap ClassMap { get; private set; }

        public BsonDocument Original { get; private set; }

        public object Current { get; private set; }

        public NonTrackedObject(BsonClassMap classMap, object current, BsonDocument original)
        {
            ClassMap = classMap;
            Current = current;
            Original = original;
        }

        public void AcceptChanges()
        {
            Original = Current.ToBsonDocument();
        }

        public TrackedObjectUpdateDocument CalculateUpdate()
        {
            var query = new QueryDocument("_id", BsonValue.Create(ClassMap.IdMemberMap.Getter(Current)));

            var update = new UpdateDocument(Current.ToBsonDocument().Elements);

            return new TrackedObjectUpdateDocument(query, update);
        }
    }
}
