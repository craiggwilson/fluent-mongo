using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentMongo.Linq.Expressions;
using MongoDB.Bson;
using System.Linq.Expressions;

namespace FluentMongo.Linq.QueryFormatters
{
    internal class BinaryExpressionDenester : MongoExpressionVisitor
    {
        private Stack<Expression> _expressions;
        private HashSet<ExpressionType> _types;

        public IEnumerable<Expression> GetDenestedExpressions(BinaryExpression expression, params ExpressionType[] types)
        {
            _expressions = new Stack<Expression>();
            _types = new HashSet<ExpressionType>(types);
            Visit(expression);

            return _expressions.Reverse();
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            _expressions.Push(b.Left);
            VisitSide(b.Left);

            _expressions.Push(b.Right);
            VisitSide(b.Right);

            return b;
        }

        private void VisitSide(Expression expression)
        {
            var b = expression as BinaryExpression;
            if (b != null && _types.Contains(b.NodeType))
            {
                _expressions.Pop();
                VisitBinary(b);
            }
        }
    }
}