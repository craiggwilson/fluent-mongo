using System.Linq;
using MongoDB.Driver;

namespace FluentMongo.Linq
{
    internal interface IMongoQueryable : IQueryable
    {
        MongoCollection Collection { get; }

        MongoQueryObject GetQueryObject();
    }
}