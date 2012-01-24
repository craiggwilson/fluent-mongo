using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;

namespace FluentMongo.Context
{
    public interface IMongoContext : IDisposable
    {
        MongoDatabase Database { get; }

        IQueryable<T> Find<T>(string collectionName);

        SafeModeResult Save<T>(string collectionName, T entity);

        SafeModeResult Save<T>(string collectionName, T entity, MongoInsertOptions options);

        SafeModeResult Save<T>(string collectionName, T entity, SafeMode safeMode);
    }
}