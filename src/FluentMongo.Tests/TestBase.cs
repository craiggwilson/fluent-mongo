using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;

using NUnit.Framework;

namespace FluentMongo
{
    public abstract class TestBase
    {
        private MongoServer _server;
        private MongoDatabase _database;

        [TestFixtureSetUp]
        public virtual void SetupFixture()
        {
            _server = MongoServer.Create("mongodb://localhost");
        }

        [SetUp]
        public virtual void SetupTest()
        {
            _database = _server.GetDatabase("test");
        }

        [TearDown]
        public virtual void TearDownTest()
        {
            _server.DropDatabase("test");
        }

        [TestFixtureTearDown]
        public virtual void TearDownFixture()
        { }

        protected MongoCollection<T> GetCollection<T>(string name)
        {
            return _database.GetCollection<T>(name);
        }
    }
}