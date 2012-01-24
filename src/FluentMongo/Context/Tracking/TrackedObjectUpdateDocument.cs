using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;

namespace FluentMongo.Context.Tracking
{
    public class TrackedObjectUpdateDocument
    {
        public IMongoQuery Query { get; private set; }

        public IMongoUpdate Update { get; private set; }

        public TrackedObjectUpdateDocument(IMongoQuery query, IMongoUpdate update)
        {
            Query = query;
            Update = update;
        }
    }
}