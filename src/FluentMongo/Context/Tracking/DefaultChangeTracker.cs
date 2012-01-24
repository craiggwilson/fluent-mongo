using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Bson.Serialization;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FluentMongo.Context.Tracking
{
    internal class DefaultChangeTracker : IChangeTracker
    {
        private Dictionary<object, DefaultTrackedObject> _items;

        public DefaultChangeTracker()
        {
            _items = new Dictionary<object, DefaultTrackedObject>();
        }

        ~DefaultChangeTracker()
        {
            Dispose(false);
        }

        public void AcceptChanges()
        {
            EnsureNotDisposed();
            var list = _items.Values.ToList();
            foreach (var obj in list)
                obj.AcceptChanges();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        public ITrackedObject GetTrackedObject(object obj)
        {
            EnsureNotDisposed();
            DefaultTrackedObject trackedObject;
            if (!_items.TryGetValue(obj, out trackedObject))
                return null;

            return trackedObject;
        }

        public bool IsTracked(object obj)
        {
            EnsureNotDisposed();
            return _items.ContainsKey(obj);
        }

        public void StopTracking(object obj)
        {
            EnsureNotDisposed();
            _items.Remove(obj);
        }

        public ITrackedObject Track(object obj)
        {
            EnsureNotDisposed();
            var trackedObject = GetTrackedObject(obj);
            if (trackedObject == null)
                trackedObject = CreateTrackedObject(obj);

            return trackedObject;
        }

        private void Dispose(bool disposing)
        {
            if (!disposing)
                return;

            _items.Clear();
            _items = null;
        }

        private ITrackedObject CreateTrackedObject(object obj)
        {
            var classMap = BsonClassMap.LookupClassMap(obj.GetType());
            if (classMap.IsAnonymous)
                return new NonTrackedObject(classMap, obj, obj.ToBsonDocument());
            var trackedObject = new DefaultTrackedObject(classMap, obj, obj.ToBsonDocument());
            _items.Add(obj, trackedObject);
            return trackedObject;
        }

        private void EnsureNotDisposed()
        {
            if (_items == null)
                throw new ObjectDisposedException(GetType().FullName);
        }

        private class DefaultTrackedObject : ITrackedObject
        {
            public BsonClassMap ClassMap { get; private set; }

            public BsonDocument Original { get; private set; }

            public object Current { get; private set; }

            public DefaultTrackedObject(BsonClassMap classMap, object current, BsonDocument original)
            {
                ClassMap = classMap;
                Current = current;
                Original = original;
            }

            public void AcceptChanges()
            {
                Original = Current.ToBsonDocument();
            }

            public TrackedObjectUpdateDocument CalculateUpdate()
            {
                var query = new QueryDocument("_id", BsonValue.Create(ClassMap.IdMemberMap.Getter(Current)));

                var currentAsDocument = Current.ToBsonDocument();

                var update = new UpdateDocumentBuilder(Original, currentAsDocument).Build();

                return new TrackedObjectUpdateDocument(query, update);
            }
        }
    }
}
