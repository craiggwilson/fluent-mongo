using System;

namespace FluentMongo.Linq.Expressions
{
    internal class ScalarExpression : SubqueryExpression
    {
        public ScalarExpression(Type type, SelectExpression select)
            : base(MongoExpressionType.Scalar, type, select)
        { }
    }
}
