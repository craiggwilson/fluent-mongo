using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.CSharpDriver;

namespace FluentMongo.Linq
{
    public static class MongoCollectionExtensions
    {

        public static IQueryable<T> AsQueryable<T>(this MongoCollection<T> collection)
        {
            return new MongoQuery<T>(new MongoQueryProvider(collection));
        }

    }
}