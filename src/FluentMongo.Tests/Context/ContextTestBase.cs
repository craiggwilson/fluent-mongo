using NUnit.Framework;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace FluentMongo.Context
{
    public class ContextTestBase : TestBase
    {
        protected MongoCollection<Person> Collection;

        public override void SetupFixture()
        {
            base.SetupFixture();

            Collection = GetCollection<Person>("people");
        }

        public void RegisterClassMapIfNecessary<TClassMap>() where TClassMap : BsonClassMap, new()
        {
            RegisterClassMapIfNecessary<TClassMap>(new TClassMap());
        }

        public void RegisterClassMapIfNecessary<TClassMap>(TClassMap classMap) where TClassMap : BsonClassMap
        {
            if (!BsonClassMap.IsClassMapRegistered(classMap.ClassType))
                BsonClassMap.RegisterClassMap(classMap);
        }

        protected IMongoContext CreateContext()
        {
            return new MongoContext(_database);
        }
    }
}