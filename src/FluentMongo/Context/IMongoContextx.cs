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

        void Insert<T>(string collectionName, T entity);

        void Insert<T>(string collectionName, T entity, MongoInsertOptions options);

        void Insert<T>(string collectionName, T entity, SafeMode safeMode);

        void Update<T>(string collectionName, T entity);

        void Update<T>(string collectionName, T entity, MongoUpdateOptions options);

        void Update<T>(string collectionName, T entity, SafeMode safeMode);

        void Update<T>(string collectionName, T entity, UpdateFlags flags);

        void Update<T>(string collectionName, T entity, UpdateFlags flags, SafeMode safeMode);
    }
}