using System;
using System.Linq;
using System.Linq.Expressions;
using FluentMongo.Linq.Expressions;
using MongoDB.Bson;

namespace FluentMongo.Linq.Translators
{
    internal class ProjectionBuilder : MongoExpressionVisitor
    {
        private bool _isMapReduce;
        private ParameterExpression _document;
        private readonly GroupingKeyDeterminer _determiner;

        public ProjectionBuilder()
        {
            _determiner = new GroupingKeyDeterminer();
        }

        public LambdaExpression Build(Expression projector, Type documentType, string parameterName, bool isMapReduce)
        {
            _isMapReduce = isMapReduce;
            if (_isMapReduce)
                _document = Expression.Parameter(typeof(BsonDocument), parameterName);
            else
                _document = Expression.Parameter(documentType, parameterName);

            return Expression.Lambda(Visit(projector), _document);
        }

        protected override Expression VisitField(FieldExpression field)
        {
            if (!_isMapReduce)
                return Visit(field.Expression);

            var parts = field.Name.Split('.');

            bool isGroupingField = _determiner.IsGroupingKey(field);
            Expression current;
            if (parts.Contains("Key") && isGroupingField)
                current = _document;
            else
                current = Expression.Call(
                            _document,
                            "GetValue",
                            Type.EmptyTypes,
                            Expression.Constant("value"));

            for (int i = 0; i < parts.Length; i++)
            {
                if (parts[i] == "Key" && isGroupingField)
                    parts[i] = "_id";

                current = Expression.Call(
                        Expression.Convert(
                            current,
                            typeof(BsonDocument)),
                        "GetValue",
                        Type.EmptyTypes,
                        Expression.Constant(parts[i]));
            }

            return Expression.Convert(
                current = Expression.Call(
                    typeof(Convert),
                    "ChangeType",
                    Type.EmptyTypes,
                    Expression.MakeMemberAccess(current, typeof(BsonValue).GetProperty("RawValue")),
                    Expression.Constant(field.Type)),
                field.Type);
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            return _document;
        }

        private class GroupingKeyDeterminer : MongoExpressionVisitor
        {
            private bool _isGroupingKey;

            public bool IsGroupingKey(Expression exp)
            {
                _isGroupingKey = false;
                Visit(exp);
                return _isGroupingKey;
            }

            protected override Expression Visit(Expression exp)
            {
                if (exp == null)
                    return exp;

                if (_isGroupingKey)
                    return exp;

                if (exp.Type.IsGenericType && exp.Type.GetGenericTypeDefinition() == typeof(Grouping<,>))
                {
                    _isGroupingKey = true;
                    return exp;
                }
                return base.Visit(exp);
            }
        }
    }
}