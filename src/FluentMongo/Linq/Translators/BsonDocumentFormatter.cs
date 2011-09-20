﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text.RegularExpressions;

using FluentMongo.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using MongoDB.Bson.IO;

namespace FluentMongo.Linq.Translators
{
    internal class BsonDocumentFormatter : MongoExpressionVisitor
    {
        private BsonDocument _query;
        private Stack<Scope> _scopes;
        private bool _hasPredicate;

        internal BsonDocument FormatDocument(Expression expression)
        {
            _query = new BsonDocument();
            _scopes = new Stack<Scope>();
            Visit(expression);
            return _query;
        }

        protected override Expression VisitBinary(BinaryExpression b)
        {
            int scopeDepth = _scopes.Count;
            bool hasPredicate = b.NodeType != ExpressionType.And && b.NodeType != ExpressionType.AndAlso && b.NodeType != ExpressionType.Or && b.NodeType != ExpressionType.OrElse;

            if (b.NodeType == ExpressionType.Or || b.NodeType == ExpressionType.OrElse)
                PushConditionScope("$or");

            VisitPredicate(b.Left, hasPredicate);

            switch (b.NodeType)
            {
                case ExpressionType.Equal:
                    break;
                case ExpressionType.GreaterThan:
                    PushConditionScope("$gt");
                    break;
                case ExpressionType.GreaterThanOrEqual:
                    PushConditionScope("$gte");
                    break;
                case ExpressionType.LessThan:
                    PushConditionScope("$lt");
                    break;
                case ExpressionType.LessThanOrEqual:
                    PushConditionScope("$lte");
                    break;
                case ExpressionType.NotEqual:
                    PushConditionScope("$ne");
                    break;
                case ExpressionType.Modulo:
                    throw new NotImplementedException();
                case ExpressionType.Or:
                case ExpressionType.OrElse:
                    break;
                case ExpressionType.And:
                case ExpressionType.AndAlso:
                    break;
                default:
                    throw new NotSupportedException(string.Format("The operation {0} is not supported.", b.NodeType));
            }

            VisitPredicate(b.Right, false);

            while (_scopes.Count > scopeDepth)
                PopConditionScope();

            return b;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            AddCondition(c.Value);
            return c;
        }

        protected override Expression VisitField(FieldExpression f)
        {
            if (!_hasPredicate)
            {
                PushConditionScope(f.Name, f.MemberMap);
                AddCondition(true);
                PopConditionScope();
            }
            else
                PushConditionScope(f.Name, f.MemberMap);
            return f;
        }

        protected override Expression VisitMemberAccess(MemberExpression m)
        {
            if (m.Member.DeclaringType == typeof(Array))
            {
                if (m.Member.Name == "Length")
                {
                    VisitPredicate(m.Expression, true);
                    PushConditionScope("$size");
                    return m;
                }
            }
            else if (typeof(ICollection).IsAssignableFrom(m.Member.DeclaringType))
            {
                if (m.Member.Name == "Count")
                {
                    VisitPredicate(m.Expression, true);
                    PushConditionScope("$size");
                    return m;
                }
            }
            else if (typeof(ICollection<>).IsOpenTypeAssignableFrom(m.Member.DeclaringType))
            {
                if (m.Member.Name == "Count")
                {
                    VisitPredicate(m.Expression, true);
                    PushConditionScope("$size");
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
                        if(m.Arguments.Count != 2)
                            throw new NotSupportedException("Only the Any method with 2 arguments is supported.");

                        field = m.Arguments[0] as FieldExpression;
                        if (field == null)
                            throw new InvalidQueryException("A mongo field must be a part of the Contains method.");
                        VisitPredicate(field, true);
                        PushConditionScope("$elemMatch");
                        VisitPredicate(m.Arguments[1], true);
                        PopConditionScope(); //elemMatch
                        PopConditionScope(); //field
                        return m;
                    case "Contains":
                        if (m.Arguments.Count != 2)
                            throw new NotSupportedException("Only the Contains method with 2 arguments is supported.");

                        field = m.Arguments[0] as FieldExpression;
                        if (field != null)
                        {
                            VisitPredicate(field, true);
                            AddCondition(EvaluateConstant<object>(m.Arguments[1]));
                            PopConditionScope();
                            return m;
                        }

                        field = m.Arguments[1] as FieldExpression;
                        if (field == null)
                            throw new InvalidQueryException("A mongo field must be a part of the Contains method.");
                        VisitPredicate(field, true);
                        AddCondition("$in", EvaluateConstant<IEnumerable>(m.Arguments[0]));
                        PopConditionScope();
                        return m;
                    case "Count":
                        if (m.Arguments.Count == 1)
                        {
                            Visit(m.Arguments[0]);
                            PushConditionScope("$size");
                            return m;
                        }
                        throw new NotSupportedException("The method Count with a predicate is not supported for field.");
                }
            }
            else if(typeof(ICollection<>).IsOpenTypeAssignableFrom(m.Method.DeclaringType) || typeof(IList).IsAssignableFrom(m.Method.DeclaringType))
            {
                switch(m.Method.Name)
                {
                    case "Contains":
                        field = m.Arguments[0] as FieldExpression;
                        if (field != null)
                        {
                            VisitPredicate(field, true);
                            AddCondition("$in", EvaluateConstant<IEnumerable>(m.Object).OfType<object>().ToArray());
                            PopConditionScope();
                            return m;
                        }

                        field = m.Object as FieldExpression;
                        if(field == null)
                            throw new InvalidQueryException("A mongo field must be a part of the Contains method.");
                        if (m.Arguments.Count != 1)
                            throw new NotSupportedException("Only the Contains method with 1 argument is supported.");

                        VisitPredicate(field, true);
                        AddCondition(EvaluateConstant<object>(m.Arguments[0]));
                        PopConditionScope();
                        return m;
                }
            }
            else if (m.Method.DeclaringType == typeof(string))
            {
                field = m.Object as FieldExpression;
                if (field == null)
                    throw new InvalidQueryException(string.Format("The mongo field must be the operator for a string operation of type {0}.", m.Method.Name));
                VisitPredicate(field, true);

                var value = Regex.Escape(EvaluateConstant<string>(m.Arguments[0]));
                
                switch(m.Method.Name)
                {
                    case "StartsWith":
                        AddCondition(new BsonRegularExpression(string.Format("^{0}", value)));
                        break;
                    case "EndsWith":
                        AddCondition(new BsonRegularExpression(string.Format("{0}$", value)));
                        break;
                    case "Contains":
                        AddCondition(new BsonRegularExpression(string.Format("{0}", value)));
                        break;
                    default:
                        throw new NotSupportedException(string.Format("The string method {0} is not supported.", m.Method.Name));
                }

                PopConditionScope();
                return m;
            }
            else if (m.Method.DeclaringType == typeof(Regex))
            {
                if (m.Method.Name == "IsMatch")
                {
                    field = m.Arguments[0] as FieldExpression;
                    if (field == null)
                        throw new InvalidQueryException(string.Format("The mongo field must be the operator for a string operation of type {0}.", m.Method.Name));

                    VisitPredicate(field, true);
                    string value;
                    if (m.Object == null)
                        value = EvaluateConstant<string>(m.Arguments[1]);
                    else
                        throw new InvalidQueryException(string.Format("Only the static Regex.IsMatch is supported.", m.Method.Name));

                    var regexOptions = RegexOptions.None;
                    if (m.Arguments.Count > 2)
                        regexOptions = EvaluateConstant<RegexOptions>(m.Arguments[2]);

                    AddCondition(new BsonRegularExpression(new Regex(value, regexOptions)));
                    PopConditionScope();
                    return m;
                }
            }

            throw new NotSupportedException(string.Format("The method {0} is not supported.", m.Method.Name));
        }

        protected override Expression VisitUnary(UnaryExpression u)
        {
            switch (u.NodeType)
            {
                case ExpressionType.Not:
                    if (u.Operand is FieldExpression)
                    {
                        VisitPredicate(u.Operand, true);
                        AddCondition(false);
                        PopConditionScope();
                    }
                    else
                    {
                        PushConditionScope("$not");
                        VisitPredicate(u.Operand, false);
                        PopConditionScope();
                    }
                    break;
                case ExpressionType.ArrayLength:
                    Visit(u.Operand);
                    PushConditionScope("$size");
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

        protected override Expression VisitTypeIs(TypeBinaryExpression b)
        {
            var convention = MongoDB.Bson.Serialization.BsonDefaultSerializer.LookupDiscriminatorConvention(b.TypeOperand);
            var discriminator = convention.GetDiscriminator(b.Expression.Type, b.TypeOperand);

            AddCondition(convention.ElementName, discriminator);

            return b;
        }

        private void AddCondition(object value)
        {
            var currentScope = _scopes.Peek();

            if (value != null && currentScope.MemberMap != null)
            {
                try
                {
                    var memberType = currentScope.MemberMap.MemberType;

                    // if the member type closes IEnumerable<> then find the element type
                    if (typeof(IEnumerable<>).IsOpenTypeAssignableFrom(memberType))
                        memberType = memberType.GetInterfaceClosing(typeof(IEnumerable<>)).GetGenericArguments()[0];

                    // if the current type is not the MemberType and is IEnumerable, then it might be a $in query
                    if (memberType != value.GetType() && value is IEnumerable)
                    {
                        value = ((IEnumerable)value).OfType<object>().Select(v => serializeValue(v, currentScope.MemberMap)).ToArray();
                    }
                    else
                    {
                        value = serializeValue(value, currentScope.MemberMap);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidQueryException("An error occured when converting value with the serializer", ex);
                }
            }

            currentScope.AddCondition(value ?? BsonNull.Value);
        }

        private object serializeValue(object value, BsonMemberMap memberMap)
        {
            const string tmpField = "tmp";

            var type = value.GetType();
            var serializer = memberMap.GetSerializer(type);

            var document = new BsonDocument();
            using (var writer = BsonWriter.Create(document))
            {
                // serialize the value inside a document using the provided serializer
                writer.WriteStartDocument();
                writer.WriteName(tmpField);
                serializer.Serialize(writer, type, value, memberMap.SerializationOptions);
                writer.WriteEndDocument();
            }

            // extract the serialized value from the document
            return document[tmpField];
        }

        private void AddCondition(string name, object value)
        {
            PushConditionScope(name);
            AddCondition(value);
            PopConditionScope();
        }

        private void PushConditionScope(string name)
        {
            PushConditionScope(name, null);
        }

        private void PushConditionScope(string name, BsonMemberMap memberMap)
        {
            if (_scopes.Count == 0)
            {
                object value = null;
                if (_query.Contains(name))
                    value = _query[name];
                _scopes.Push(new Scope(name, value, memberMap));
            }
            else
                _scopes.Push(_scopes.Peek().CreateChildScope(name, memberMap));
        }

        private void PopConditionScope()
        {
            var scope = _scopes.Pop();
            if (scope.Value == null)
                return;

            if (scope.Key == "$elemMatch")
            {
                _scopes.Peek().AddCondition(scope.Value);
                return;
            }

            var doc = _query;
            foreach (var s in _scopes.Reverse()) //as if it were a queue
            {
                BsonValue sub;
                if (!doc.TryGetValue(s.Key, out sub))
                    doc[s.Key] = sub = new BsonDocument();
                else if (!(sub is BsonDocument))
                    throw new InvalidQueryException();

                doc = (BsonDocument)sub;
            }

            if (_scopes.Count > 0 && _scopes.Peek().Key == "$or")
            {
                BsonArray arr = _scopes.Peek().Value as BsonArray;
                if (arr == null)
                    arr = new BsonArray();

                arr.Add(new BsonDocument(scope.Key, BsonValue.Create(scope.Value)));
                _scopes.Peek().AddCondition(arr);
            }
            else
            {
                var valueType = scope.Value.GetType();
                object value = scope.Value;
                if(valueType != typeof(string) && !typeof(BsonValue).IsAssignableFrom(valueType) && typeof(IEnumerable).IsAssignableFrom(valueType))
                    value = ((IEnumerable)scope.Value).Cast<object>();
                doc[scope.Key] = BsonValue.Create(value);
            }
        }

        private void VisitPredicate(Expression expression, bool hasPredicate)
        {
            var oldHasPredicate = _hasPredicate;
            _hasPredicate = hasPredicate;
            Visit(expression);
            _hasPredicate = oldHasPredicate;
        }

        private static T EvaluateConstant<T>(Expression e)
        {
            if (e.NodeType != ExpressionType.Constant)
                throw new ArgumentException("Expression must be a constant.");

            return (T)((ConstantExpression)e).Value;
        }

        private static bool IsBoolean(Expression expression)
        {
            return expression.Type == typeof(bool) || expression.Type == typeof(bool?);
        }

        private class Scope
        {
            public string Key { get; private set; }

            public object Value { get; private set; }

            public BsonMemberMap MemberMap { get; private set; }

            public Scope(string key, object initialValue, BsonMemberMap memberMap)
            {
                Key = key;
                Value = initialValue;
                MemberMap = memberMap;
            }

            public void AddCondition(object value)
            {
                if (Value is BsonDocument)
                {
                    if (!(value is BsonDocument))
                        throw new InvalidQueryException();

                    ((BsonDocument)Value).Merge((BsonDocument)value);
                }
                else
                    Value = value;
            }

            public Scope CreateChildScope(string name, BsonMemberMap memberMap)
            {
                if (Value is BsonDocument)
                {
                    BsonValue currentValue;
                    if(!((BsonDocument)Value).TryGetValue(name, out currentValue))
                        currentValue = null;
                    return new Scope(name, currentValue, memberMap ?? MemberMap);
                }

                return new Scope(name, null, memberMap ?? MemberMap);
            }
        }
    }
}