using NUnit.Framework;
using MongoDB.Driver;
using MongoDB.Bson;

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
    }
}
