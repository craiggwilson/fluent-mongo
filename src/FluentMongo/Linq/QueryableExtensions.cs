using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;

namespace FluentMongo.Linq
{
    public static class QueryableExtensions
    {
        public static IQueryable<T> QueryDump<T>(this IQueryable<T> q, Action<Dictionary<string,string>> dumpTarget)
        {
            var mongoQueryable = q as IMongoQueryable;
            if (mongoQueryable == null || dumpTarget == null)
            {
                return q;
            }

            var dump = new Dictionary<string, string>();
            var command = mongoQueryable.GetQueryObject();

            if (command == null)
            {
                return q;
            }

            dump.Add("database",command.Collection.Database.Name);
            dump.Add("collection",command.Collection.Name);
            dump.Add("type",command.DocumentType.FullName);

            if (command.Query != null)
                dump.Add("query", command.Query.ToJson());
            if (command.MapFunction != null)
                dump.Add("map", command.MapFunction.ToJson());
            if (command.ReduceFunction != null)
                dump.Add("reduce", command.ReduceFunction.ToJson());
            if (command.Sort != null)
                dump.Add("sort", command.Sort.ToJson());
                
            dumpTarget(dump);
                
            return q;
        }
    }
}
