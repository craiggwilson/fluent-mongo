using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using MongoDB.Driver;

namespace FluentMongo.Context
{
    [TestFixture]
    public class MongoContextTests : ContextTestBase
    {
        public override void SetupFixture()
        {
            base.SetupFixture();

            Collection.Insert(
                new Person
                {
                    FirstName = "Bob",
                    LastName = "McBob",
                }, SafeMode.True);

            Collection.Insert(
                new Person
                {
                    FirstName = "Jane",
                    LastName = "McJane",
                },
                SafeMode.True);

            Collection.Insert(
                new Person
                {
                    FirstName = "Joe",
                    LastName = "McJoe",
                },
                SafeMode.True);

            Collection.Insert(
                new Person
                {
                    FirstName = "Dave",
                    LastName = "McDave",
                },
                SafeMode.True);
        }

        [Test]
        public void Update_should_only_issue_updated_query()
        {
            using (var context = CreateContext())
            {
                var people = context.Find<Person>("people").Where(x => x.FirstName == "Joe").ToList();

                people[0].FirstName = "Jim";

                context.Update("people", people[0]);
            }
        }
    }
}