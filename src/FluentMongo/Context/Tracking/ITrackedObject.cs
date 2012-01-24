using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;

namespace FluentMongo.Context.Tracking
{
    internal interface ITrackedObject
    {
        BsonClassMap ClassMap { get; }

        BsonDocument Original { get; }

        object Current { get; }

        void AcceptChanges();

        TrackedObjectUpdateDocument CalculateUpdate();
    }
}