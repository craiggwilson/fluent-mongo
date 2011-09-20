using NUnit.Framework;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;

namespace FluentMongo.Linq
{
    public class LinqTestBase : TestBase
    {
        protected MongoCollection<Person> Collection;
        protected MongoCollection<BsonDocument> BsonDocumentCollection;

        public override void SetupFixture()
        {
            base.SetupFixture();

            Collection = GetCollection<Person>("people");
            BsonDocumentCollection = GetCollection<BsonDocument>("people");
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
    }
}
