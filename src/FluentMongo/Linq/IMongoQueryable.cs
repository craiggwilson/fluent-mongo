using System.Linq;
using MongoDB.CSharpDriver;

namespace FluentMongo.Linq
{
    internal interface IMongoQueryable : IQueryable
    {
        MongoCollection Collection { get; }

        MongoQueryObject GetQueryObject();
    }
}