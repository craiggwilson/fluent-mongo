using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson;

namespace FluentMongo.Linq
{
    public static class QueryableExtensions
    {
        public static IDictionary<string, string> Explain<T>(this IQueryable<T> q)
        {
            var explanation = new Dictionary<string, string>();
            var mongoQueryable = q as IMongoQueryable;
            if (mongoQueryable == null)
                return explanation;

            q.QueryDump(d => explanation = d);
            return explanation;
        }

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
            if(command.Fields != null && command.Fields.ElementCount > 0)
                dump.Add("fields", command.Fields.ToJson());
            if (command.MapFunction != null)
                dump.Add("map", command.MapFunction.ToJson());
            if (command.ReduceFunction != null)
                dump.Add("reduce", command.ReduceFunction.ToJson());
            if (command.Sort != null)
                dump.Add("sort", command.Sort.ToJson());

            dump.Add("skip", command.NumberToSkip.ToString());
            dump.Add("limit", command.NumberToLimit.ToString());
            dump.Add("isCount", command.IsCount.ToString());

                
            dumpTarget(dump);
                
            return q;
        }
    }
}
