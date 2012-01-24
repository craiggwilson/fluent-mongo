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

        public bool IsDeleted
        {
            get { return false; }
        }

        public bool IsNew
        {
            get { return false; }
        }

        public bool IsModified
        {
            get { return false; }
        }

        public bool IsPossiblyModified
        {
            get { return false; }
        }

        public TrackedObjectUpdateDocument CalculateUpdate()
        {
            var query = new QueryDocument("_id", BsonValue.Create(ClassMap.IdMemberMap.Getter(Current)));

            var update = new UpdateDocument(Current.ToBsonDocument().Elements);

            return new TrackedObjectUpdateDocument(query, update);
        }

        public void ConvertToDead()
        {
            //do nothing
        }

        public void ConvertToDeleted()
        {
            //do nothing
        }

        public void ConvertToModified()
        {
            //do nothing
        }

        public void ConvertToNew()
        {
            //do nothing
        }

        public void ConvertToPossiblyModified()
        {
            //do nothing
        }

        public void ConvertToUnmodified()
        {
            //do nothing
        }
    }
}
