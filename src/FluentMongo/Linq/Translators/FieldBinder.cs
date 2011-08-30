using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using FluentMongo.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using FluentMongo.Linq.Util;
using MongoDB.Bson.Serialization;
using System.Reflection;

namespace FluentMongo.Linq.Translators
{
    internal class FieldBinder : ExpressionVisitor
    {
        private static readonly HashSet<Type> CollectionTypes = new HashSet<Type>
        {
            typeof(ICollection), typeof(ICollection<>)
        };

        private Alias _alias;
        private FieldFinder _finder;
        private Type _elementType;
        private Dictionary<MemberInfo, Expression> _memberMap;

        public Expression Bind(Expression expression, Type elementType)
        {
            _alias = new Alias();
            _memberMap = new Dictionary<MemberInfo, Expression>();
            _finder = new FieldFinder(_memberMap);
            _elementType = elementType;
            return Visit(expression);
        }

        protected override Expression Visit(Expression exp)
        {
            if (exp == null)
                return exp;

            var result = _finder.Find(exp);
            if (result != null)
                return new FieldExpression(exp, _alias, result.FieldName, result.MemberMap);

            return base.Visit(exp);
        }

        protected override NewExpression VisitNew(NewExpression nex)
        {
            var newNex = base.VisitNew(nex);
            if (newNex != nex && newNex.Members != null) //we only get Members when it is an anonymous type
            {
                var properties = newNex.Type.GetProperties();
                var joined = from param in newNex.Constructor.GetParameters()
                             join prop in properties on param.Name equals prop.Name
                             select new { Parameter = param, Property = prop };

                foreach (var j in joined)
                    _memberMap[j.Property] = newNex.Arguments[j.Parameter.Position];
            }
            return newNex;
        }

        protected override Expression VisitParameter(ParameterExpression p)
        {
            if(p.Type == _elementType)
                return new FieldExpression(p, _alias, "*");

            return base.VisitParameter(p);
        }

        private class FieldFinder : ExpressionVisitor
        {
            public class FindResult
            {
                public string FieldName;
                public BsonMemberMap MemberMap;
            }

            private Stack<string> _fieldParts;
            private bool _isBlocked;
            private BsonMemberMap _bsonMemberMap;
            private readonly Dictionary<MemberInfo, Expression> _memberMap;
            private readonly GroupingKeyDeterminer _determiner;

            public FieldFinder(Dictionary<MemberInfo, Expression> memberMap)
            {
                _determiner = new GroupingKeyDeterminer();
                _memberMap = memberMap;
            }

            public FindResult Find(Expression expression)
            {
                if (expression.NodeType == ExpressionType.Parameter)
                    return null;

                _fieldParts = new Stack<string>();
                _isBlocked = false;
                _bsonMemberMap = null;
                Visit(expression);
                var fieldName = string.Join(".", _fieldParts.ToArray());

                if (_isBlocked)
                    return null;

                return new FindResult { FieldName = fieldName, MemberMap = _bsonMemberMap };
            }

            protected override Expression Visit(Expression exp)
            {
                if (exp == null)
                    return null;

                switch (exp.NodeType)
                {
                    case ExpressionType.ArrayIndex:
                    case ExpressionType.Call:
                    case ExpressionType.MemberAccess:
                    case ExpressionType.Parameter:
                        return base.Visit(exp);
                    default:
                        _isBlocked = true;
                        return exp;
                }
            }

            protected override Expression VisitBinary(BinaryExpression b)
            {
                //this is an ArrayIndex Node
                _fieldParts.Push(((int)((ConstantExpression)b.Right).Value).ToString());
                Visit(b.Left);
                return b;
            }

            protected override Expression VisitMemberAccess(MemberExpression m)
            {
                var declaringType = m.Member.DeclaringType;
                if (!TypeHelper.IsNativeToMongo(declaringType) && !IsCollection(declaringType))
                {
                    Expression e;
                    if (_determiner.IsGroupingKey(m))
                    {
                        _fieldParts.Push(m.Member.Name);
                        Visit(m.Expression);
                        return m;
                    }
                    else if (_memberMap.TryGetValue(m.Member, out e) && e is FieldExpression)
                    {
                        var field = (FieldExpression)e;
                        _fieldParts.Push(field.Name);
                        _bsonMemberMap = field.MemberMap;
                        Visit(m.Expression);
                        return m;
                    }
                    else if(e == null)
                    {
                        var classMap = BsonClassMap.LookupClassMap(declaringType);
                        var propMap = classMap.GetMemberMap(m.Member.Name);
                        if (propMap != null)
                        {
                            _fieldParts.Push(propMap.ElementName);
                            _bsonMemberMap = propMap;
                        }
                        else
                            _fieldParts.Push(m.Member.Name);

                        Visit(m.Expression);
                        return m;
                    }
                }

                _isBlocked = true;
                return m;
            }

            protected override Expression VisitMethodCall(MethodCallExpression m)
            {
                if (m.Method.DeclaringType == typeof(Queryable) || m.Method.DeclaringType == typeof(Enumerable))
                {
                    if (m.Method.Name == "ElementAt" || m.Method.Name == "ElementAtOrDefault")
                    {
                        _fieldParts.Push(((int)((ConstantExpression)m.Arguments[1]).Value).ToString());
                        Visit(m.Arguments[0]);
                        return m;
                    }
                }
                else if (m.Method.DeclaringType == typeof(MongoQueryable))
                {
                    if (m.Method.Name == "Key")
                    {
                        _fieldParts.Push((string)((ConstantExpression)m.Arguments[1]).Value);
                        Visit(m.Arguments[0]);
                        return m;
                    }
                }
                else if (typeof(BsonDocument).IsAssignableFrom(m.Method.DeclaringType))
                {
                    if (m.Method.Name == "get_Item") //TODO: does this work for VB?
                    {
                        _fieldParts.Push((string)((ConstantExpression)m.Arguments[0]).Value);
                        Visit(m.Object);
                        return m;
                    }
                }
                else if (typeof(IList<>).IsOpenTypeAssignableFrom(m.Method.DeclaringType) || typeof(IList).IsAssignableFrom(m.Method.DeclaringType))
                {
                    if (m.Method.Name == "get_Item")
                    {
                        _fieldParts.Push(((int)((ConstantExpression)m.Arguments[0]).Value).ToString());
                        Visit(m.Object);
                        return m;
                    }
                }

                _isBlocked = true;
                return m;
            }

            //protected override Expression VisitParameter(ParameterExpression p)
            //{
            //    if (p.Type.IsGenericType && p.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>))
            //        _isBlocked = true;
            //    return base.VisitParameter(p);
            //}

            private static bool IsCollection(Type type)
            {
                //HACK: this is going to generally subvert custom objects that implement ICollection or ICollection<T>, 
                //but are not collections
                if (type.IsGenericType)
                    type = type.GetGenericTypeDefinition();

                return CollectionTypes.Any(x => x.IsAssignableFrom(type));
            }
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

                if (exp.Type.IsGenericType && exp.Type.GetGenericTypeDefinition() == typeof(IGrouping<,>))
                {
                    _isGroupingKey = true;
                    return exp;
                }
                return base.Visit(exp);
            }

            protected override Expression VisitMemberAccess(MemberExpression m)
            {
                Visit(m.Expression);
                return m;
            }
        }
    }
}