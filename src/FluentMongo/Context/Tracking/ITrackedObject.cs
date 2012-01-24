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

        bool IsDeleted { get; }

        bool IsNew { get; }

        bool IsModified { get; }

        bool IsPossiblyModified { get; }

        TrackedObjectUpdateDocument CalculateUpdate();

        void ConvertToDead();

        void ConvertToDeleted();

        void ConvertToModified();

        void ConvertToNew();

        void ConvertToPossiblyModified();

        void ConvertToUnmodified();
    }
}