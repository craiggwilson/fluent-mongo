using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentMongo.Linq.Expressions;
using MongoDB.Bson;
using System.Linq.Expressions;
using MongoDB.Bson.Serialization;
using System.Collections;
using System.Text.RegularExpressions;

namespace FluentMongo.Linq.QueryFormatters
{
    internal class BsonElementsFormatter : MongoExpressionVisitor
    {
        private List<BsonElement> _elements;
        private FieldExpression _lastField;
        private Stack<string> _elementNameStack;
        private bool _hasPredicate;

        internal IEnumerable<BsonElement> GetElements(Expression expression)
        {
            _elements = new List<BsonElement>();
            _elementNameStack = new Stack<string>();
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
                    VisitBinaryAnd(node);
                    return node;
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    VisitBinaryOr(node);
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

            VisitPredicate(node.Left);

            var value = new BsonValueFormatter().GetValue(_lastField.MemberMap, node.Right);
            if (operandName == null) //equals
                AddElementWithValue(value);
            else
                AddElementWithValue(new BsonDocument(operandName, value));

            return node;
        }

        protected override Expression VisitField(FieldExpression field)
        {
            _lastField = field;
            PushElementName(field.Name);
            if (!_hasPredicate)
                AddElementWithValue(true);
            return field;
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            if (m.Member.DeclaringType == typeof(Array))
            {
                if (m.Member.Name == "Length")
                {
                    PushElementName("$size");
                    AddElementWithValue(m.Expression);
                    return m;
                }
            }
            else if (typeof(ICollection).IsAssignableFrom(m.Member.DeclaringType))
            {
                if (m.Member.Name == "Count")
                {
                    PushElementName("$size");
                    AddElementWithValue(m.Expression);
                    return m;
                }
            }
            else if (typeof(ICollection<>).IsOpenTypeAssignableFrom(m.Member.DeclaringType))
            {
                if (m.Member.Name == "Count")
                {
                    PushElementName("$size");
                    AddElementWithValue(m.Expression);
                    return m;
                }
            }

            throw new NotSupportedException(string.Format("The member {0} is not supported.", m.Member.Name));
        }

        protected override Expression VisitMethodCall(MethodCallExpression m)
        {
            FieldExpression field;
            if (m.Method.DeclaringType == typeof(Queryable) || m.Method.DeclaringType == typeof(Enumerable))
            {
                switch (m.Method.Name)
                {
                    case "Any":
                        if (m.Arguments.Count != 2)
                            throw new NotSupportedException("Only the Any method with 2 arguments is supported.");

                        field = m.Arguments[0] as FieldExpression;
                        if (field == null)
                            throw new InvalidQueryException("A mongo field must be a part of the Contains method.");

                        VisitPredicate(field);
                        PushElementName("$elemMatch");
                        Visit(m.Arguments[1]);
                        return m;
                    case "Contains":
                        if (m.Arguments.Count != 2)
                            throw new NotSupportedException("Only the Contains method with 2 arguments is supported.");

                        field = m.Arguments[0] as FieldExpression;
                        if (field != null)
                        {
                            VisitPredicate(field);
                            AddElementWithValue(m.Arguments[1]);
                            return m;
                        }

                        field = m.Arguments[1] as FieldExpression;
                        if (field == null)
                            throw new InvalidQueryException("A mongo field must be a part of the Contains method.");

                        VisitPredicate(field);
                        PushElementName("$in");
                        AddElementWithValue(m.Arguments[0]);
                        return m;
                    case "Count":
                        if (m.Arguments.Count == 1)
                        {
                            Visit(m.Arguments[0]);
                            PushElementName("$size");
                            return m;
                        }
                        throw new NotSupportedException("The method Count with a predicate is not supported for field.");
                }
            }
            else if (typeof(ICollection<>).IsOpenTypeAssignableFrom(m.Method.DeclaringType) || typeof(IList).IsAssignableFrom(m.Method.DeclaringType))
            {
                switch (m.Method.Name)
                {
                    case "Contains":
                        field = m.Arguments[0] as FieldExpression;
                        if (field != null)
                        {
                            VisitPredicate(field);
                            PushElementName("$in");
                            AddElementWithValue(m.Object);
                            return m;
                        }

                        field = m.Object as FieldExpression;
                        if (field == null)
                            throw new InvalidQueryException("A mongo field must be a part of the Contains method.");
                        if (m.Arguments.Count != 1)
                            throw new NotSupportedException("Only the Contains method with 1 argument is supported.");

                        VisitPredicate(field);
                        AddElementWithValue(m.Arguments[0]);
                        return m;
                }
            }
            else if (m.Method.DeclaringType == typeof(string))
            {
                field = m.Object as FieldExpression;
                if (field == null)
                    throw new InvalidQueryException(string.Format("The mongo field must be the operator for a string operation of type {0}.", m.Method.Name));
                VisitPredicate(field);

                var value = Regex.Escape(EvaluateConstant<string>(m.Arguments[0]));

                switch (m.Method.Name)
                {
                    case "StartsWith":
                        AddElementWithValue(new BsonRegularExpression(string.Format("^{0}", value)));
                        break;
                    case "EndsWith":
                        AddElementWithValue(new BsonRegularExpression(string.Format("{0}$", value)));
                        break;
                    case "Contains":
                        AddElementWithValue(new BsonRegularExpression(string.Format("{0}", value)));
                        break;
                    default:
                        throw new NotSupportedException(string.Format("The string method {0} is not supported.", m.Method.Name));
                }

                return m;
            }
            else if (m.Method.DeclaringType == typeof(Regex))
            {
                if (m.Method.Name == "IsMatch")
                {
                    field = m.Arguments[0] as FieldExpression;
                    if (field == null)
                        throw new InvalidQueryException(string.Format("The mongo field must be the operator for a string operation of type {0}.", m.Method.Name));

                    VisitPredicate(field);
                    string value;
                    if (m.Object == null)
                        value = EvaluateConstant<string>(m.Arguments[1]);
                    else
                        throw new InvalidQueryException(string.Format("Only the static Regex.IsMatch is supported.", m.Method.Name));

                    var regexOptions = RegexOptions.None;
                    if (m.Arguments.Count > 2)
                        regexOptions = EvaluateConstant<RegexOptions>(m.Arguments[2]);

                    AddElementWithValue(new BsonRegularExpression(new Regex(value, regexOptions)));
                    return m;
                }
            }

            throw new NotSupportedException(string.Format("The method {0} is not supported.", m.Method.Name));
        }

        protected override Expression VisitTypeIs(TypeBinaryExpression b)
        {
            var convention = BsonDefaultSerializer.LookupDiscriminatorConvention(b.TypeOperand);
            var discriminator = convention.GetDiscriminator(b.Expression.Type, b.TypeOperand);

            PushElementName(convention.ElementName);
            AddElementWithValue(discriminator);

            return b;
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    if (u.Operand is FieldExpression)
                    {
                        var field = (FieldExpression)u.Operand;
                        PushElementName(field.Name);
                        AddElementWithValue(false);
                    }
                    else
                    {
                        PushElementName("$not");
                        AddElementWithValue(u.Operand);
                    }
                    break;
                case ExpressionType.ArrayLength:
                    Visit(u.Operand);
                    PushElementName("$size");
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

        private void AddElementWithValue(BsonValue value)
        {
            BsonElement element = new BsonElement(_elementNameStack.Pop(), value);
            while (_elementNameStack.Count > 0)
                element = new BsonElement(_elementNameStack.Pop(), new BsonDocument(element));

            _elements.Add(element);
            _elementNameStack.Clear();
            _lastField = null;
        }

        private void AddElementWithValue(Expression value)
        {
            var memberMap = _lastField == null ? null : _lastField.MemberMap;
            var bsonValue = new BsonValueFormatter().GetValue(memberMap, value);
            AddElementWithValue(bsonValue);
        }

        private static T EvaluateConstant<T>(Expression e)
        {
            if (e.NodeType != ExpressionType.Constant)
                throw new ArgumentException("Expression must be a constant.");

            return (T)((ConstantExpression)e).Value;
        }

        private void PushElementName(string name)
        {
            _elementNameStack.Push(name);
        }

        private void VisitBinaryAnd(BinaryExpression b)
        {
            var savedElements = _elements;
            _elements = new List<BsonElement>();
            var nodes = new BinaryExpressionDenester().GetDenestedExpressions(b, ExpressionType.And, ExpressionType.AndAlso);
            foreach (var node in nodes)
                Visit(node);

            var groupedElements = _elements.GroupBy(x => x.Name);
            _elements = savedElements;
            foreach (var grouping in groupedElements)
            {
                if (grouping.Count() == 1)
                    _elements.Add(grouping.Single());
                else
                    _elements.Add(new BsonElement(grouping.Key, new BsonDocument(grouping.SelectMany(x => ((BsonDocument)x.Value).Elements))));
            }
        }

        private void VisitBinaryOr(BinaryExpression b)
        {
            var nodes = new BinaryExpressionDenester().GetDenestedExpressions(b, ExpressionType.Or, ExpressionType.OrElse);
            List<BsonValue> values = new List<BsonValue>();
            foreach (var node in nodes)
                values.Add(new BsonDocument(new BsonElementsFormatter().GetElements(node)));

            _elements.Add(new BsonElement("$or", new BsonArray(values)));
        }

        private void VisitPredicate(Expression expression)
        {
            var lastHasPredicate = _hasPredicate;
            _hasPredicate = true;
            Visit(expression);
            _hasPredicate = lastHasPredicate;
        }
    }
}