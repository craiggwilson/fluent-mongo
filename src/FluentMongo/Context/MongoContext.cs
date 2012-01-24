using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentMongo.Context.Tracking;
using MongoDB.Driver;
using FluentMongo.Linq;

namespace FluentMongo.Context
{
    public class MongoContext : IMongoContext
    {
        private IChangeTracker _changeTracker;
        private MongoDatabase _database;
        private bool _disposed;

        public MongoDatabase Database
        {
            get
            {
                EnsureNotDisposed();
                return _database;
            }
        }

        public MongoContext(MongoDatabase database)
        {
            _database = database;
            _changeTracker = new DefaultChangeTracker();
        }

        ~MongoContext()
        {
            Dispose(false);
        }

        public IQueryable<T> Find<T>(string collectionName)
        {
            EnsureNotDisposed();
            var collection = GetCollection<T>(collectionName);

            return new TrackingQuery<T>(
                new MongoQuery<T>(new TrackingQueryProvider(collection, _changeTracker)),
                _changeTracker);
        }

        public void Insert<T>(string collectionName, T entity)
        {
            _changeTracker.Track(entity);
            var collection = GetCollection<T>(collectionName);

            collection.Insert(entity);
        }

        public void Insert<T>(string collectionName, T entity, MongoInsertOptions options)
        {
            _changeTracker.Track(entity);
            var collection = GetCollection<T>(collectionName);

            collection.Insert(entity, options);
        }

        public void Insert<T>(string collectionName, T entity, SafeMode safeMode)
        {
            _changeTracker.Track(entity);
            var collection = GetCollection<T>(collectionName);

            collection.Insert(entity, safeMode);
        }

        public void Update<T>(string collectionName, T entity)
        {
            var collection = GetCollection<T>(collectionName);
            var trackedObject = _changeTracker.GetTrackedObject(entity);
            if (trackedObject == null)
                trackedObject = _changeTracker.Track(entity);

            var update = trackedObject.CalculateUpdate();
            trackedObject.AcceptChanges();

            if (update.Update.ElementCount == 0)
                return;

            collection.Update(update.Query, update.Update);
        }

        public void Update<T>(string collectionName, T entity, MongoUpdateOptions options)
        {
            var collection = GetCollection<T>(collectionName);
            var trackedObject = _changeTracker.GetTrackedObject(entity);
            if (trackedObject != null)
                trackedObject = _changeTracker.Track(entity);

            var update = trackedObject.CalculateUpdate();
            trackedObject.AcceptChanges();

            if (update.Update.ElementCount == 0)
                return;

            collection.Update(update.Query, update.Update, options);
        }

        public void Update<T>(string collectionName, T entity, SafeMode safeMode)
        {
            var collection = GetCollection<T>(collectionName);
            var trackedObject = _changeTracker.GetTrackedObject(entity);
            if (trackedObject != null)
                trackedObject = _changeTracker.Track(entity);

            var update = trackedObject.CalculateUpdate();
            trackedObject.AcceptChanges();

            if (update.Update.ElementCount == 0)
                return;

            collection.Update(update.Query, update.Update, safeMode);
        }

        public void Update<T>(string collectionName, T entity, UpdateFlags flags)
        {
            var collection = GetCollection<T>(collectionName);
            var trackedObject = _changeTracker.GetTrackedObject(entity);
            if (trackedObject != null)
                trackedObject = _changeTracker.Track(entity);

            var update = trackedObject.CalculateUpdate();
            trackedObject.AcceptChanges();

            if (update.Update.ElementCount == 0)
                return;

            collection.Update(update.Query, update.Update, flags);
        }

        public void Update<T>(string collectionName, T entity, UpdateFlags flags, SafeMode safeMode)
        {
            var collection = GetCollection<T>(collectionName);
            var trackedObject = _changeTracker.GetTrackedObject(entity);
            if (trackedObject != null)
                trackedObject = _changeTracker.Track(entity);

            var update = trackedObject.CalculateUpdate();
            trackedObject.AcceptChanges();

            if (update.Update.ElementCount == 0)
                return;

            collection.Update(update.Query, update.Update, flags, safeMode);
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual MongoCollection<T> GetCollection<T>(string collectionName)
        {
            return Database.GetCollection<T>(collectionName);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _changeTracker.Dispose();
            _changeTracker = null;
            _database = null;
            _disposed = true;
        }

        protected void EnsureNotDisposed()
        {
            if (_disposed)
                throw new ObjectDisposedException(GetType().FullName);
        }
    }
}