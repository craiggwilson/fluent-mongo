using System.Linq.Expressions;
using MongoDB.Bson.Serialization;

namespace FluentMongo.Linq.Expressions
{
    internal class FieldExpression : MongoExpression
    {
        public Alias Alias { get; private set; }

        public Expression Expression { get; private set; }

        public string Name { get; private set; }

        public BsonMemberMap MemberMap { get; private set; }

        public FieldExpression(Expression expression, Alias alias, string name)
            : this(expression, alias, name, null)
        { }

        public FieldExpression(Expression expression, Alias alias, string name, BsonMemberMap memberMap)
            : base(MongoExpressionType.Field, expression.Type)
        {
            Alias = alias;
            Expression = expression;
            Name = name;
            MemberMap = memberMap;
        }
    }
}