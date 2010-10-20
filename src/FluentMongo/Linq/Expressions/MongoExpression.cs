using System;
using System.Linq.Expressions;

namespace FluentMongo.Linq.Expressions
{
    internal abstract class MongoExpression : Expression
    {
        protected MongoExpression(MongoExpressionType nodeType, Type type)
            : base((ExpressionType)nodeType, type)
        { }
    }
}