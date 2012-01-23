using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentMongo.Linq.Expressions;
using MongoDB.Bson;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;

namespace FluentMongo.Linq.QueryFormatters
{
    internal class BsonElementsFormatter : MongoExpressionVisitor
    {
        private List<BsonElement> _elements;

        internal IEnumerable<BsonElement> GetElements(Expression expression)
        {
            _elements = new List<BsonElement>();
            Visit(expression);
            return _elements;
        }

        protected override Expression VisitBinary(BinaryExpression node)
        {
            string operandName = null;
            switch(node.NodeType)
            {
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    var savedElements = _elements;
                    _elements = new List<BsonElement>();
                    var andExpressions = new BinaryExpressionDenester().GetDenestedExpressions(node, ExpressionType.And, ExpressionType.AndAlso);
                    foreach (var andExpression in andExpressions)
                        Visit(andExpression);

                    var groupedElements = _elements.GroupBy(x => x.Name);
                    _elements = savedElements;
                    foreach (var grouping in groupedElements)
                    {
                        if (grouping.Count() == 1)
                            _elements.Add(grouping.Single());
                        else
                            _elements.Add(new BsonElement(grouping.Key, new BsonDocument(grouping.SelectMany(x => ((BsonDocument)x.Value).Elements))));
                    }

                    return node;
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    var orExpressions = new BinaryExpressionDenester().GetDenestedExpressions(node, ExpressionType.Or, ExpressionType.OrElse);
                    var orArray = BuildDocumentArray(orExpressions);
                    _elements.Add(new BsonElement("$or", orArray));
                    return node;
                case ExpressionType.Equal:
                    break;
                case ExpressionType.GreaterThan:
                    operandName = "$gt";
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    operandName = "$gte";
                    break;
                case ExpressionType.LessThan:
                    operandName = "$lt";
                    break;
                case ExpressionType.LessThanOrEqual:
                    operandName = "$lte";
                    break;
                case ExpressionType.NotEqual:
                    operandName = "$ne";
                    break;
            }

            var field = GetField(node.Left);
            var value = new BsonValueFormatter().GetValue(field.MemberMap, node.Right);
            if (operandName == null)
                _elements.Add(new BsonElement(field.Name, value));
            else
                _elements.Add(new BsonElement(field.Name, new BsonDocument(operandName, value)));

            return node;
        }

        protected override Expression VisitField(FieldExpression field)
        {
            _elements.Add(new BsonElement(field.Name, true));
            return field;
        }

        protected override Expression VisitTypeIs(TypeBinaryExpression b)
        {
            var convention = BsonDefaultSerializer.LookupDiscriminatorConvention(b.TypeOperand);
            var discriminator = convention.GetDiscriminator(b.Expression.Type, b.TypeOperand);

            _elements.Add(new BsonElement(convention.ElementName, discriminator));

            return b;
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    if (u.Operand is FieldExpression)
                    {
                        var field = GetField(u.Operand);
                        _elements.Add(new BsonElement(field.Name, false));
                    }
                    else
                    {
                        AddElementWithValue("$not", u.Operand);
                    }
                    break;
                case ExpressionType.ArrayLength:
                    AddElementWithValue("$size", u.Operand);
                    break;
                case ExpressionType.Convert:
                case ExpressionType.ConvertChecked:
                    Visit(u.Operand);
                    break;
                default:
                    throw new NotSupportedException(string.Format("The unary operator {0} is not supported.", u.NodeType));
            }

            return u;
        }

        private void AddElementWithValue(string name, Expression value)
        {
            var bsonValue = new BsonValueFormatter().GetValue(value);
            _elements.Add(new BsonElement(name, bsonValue));
        }

        private FieldExpression GetField(Expression node)
        {
            var field = node as FieldExpression;
            while(field == null)
            {
                var unary = node as UnaryExpression;
                if (unary != null)
                    field = unary.Operand as FieldExpression;
                else
                    throw new NotSupportedException("Only fields are allowed on the left hand side.");
            }

            return field;
        }

        private BsonArray BuildDocumentArray(IEnumerable<Expression> nodes)
        {
            List<BsonValue> values = new List<BsonValue>();
            foreach (var node in nodes)
                values.Add(new BsonDocument(new BsonElementsFormatter().GetElements(node)));

            return new BsonArray(values);
        }
    }
}