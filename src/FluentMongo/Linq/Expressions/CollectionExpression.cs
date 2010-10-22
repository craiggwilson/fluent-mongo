using System;
using MongoDB.Driver;

namespace FluentMongo.Linq.Expressions
{
    internal class CollectionExpression : AliasedExpression
    {
        public MongoCollection Collection { get; private set; }

        public Type DocumentType { get; private set; }

        public CollectionExpression(Alias alias, MongoCollection collection, Type documentType)
            : base(MongoExpressionType.Collection, typeof(void), alias)
        {
            Collection = collection;
            DocumentType = documentType;
        }
    }
}