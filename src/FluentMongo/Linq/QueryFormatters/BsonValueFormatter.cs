using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentMongo.Linq.Expressions;
using System.Linq.Expressions;
using MongoDB.Bson;
using MongoDB.Bson.Serialization;
using System.Collections;
using MongoDB.Bson.IO;

namespace FluentMongo.Linq.QueryFormatters
{
    internal class BsonValueFormatter : MongoExpressionVisitor
    {
        private BsonMemberMap _memberMap;
        private BsonValue _value;

        internal BsonValue GetValue(Expression expression)
        {
            return GetValue(null, expression);
        }

        internal BsonValue GetValue(BsonMemberMap memberMap, Expression expression)
        {
            _memberMap = memberMap;
            Visit(expression);
            if (_value == null)
                throw new NotSupportedException("No value result.");

            return _value;
        }

        protected override Expression VisitConstant(ConstantExpression c)
        {
            CreateBsonValue(c.Value);
            return c;
        }

        private void CreateBsonValue(object value)
        {
            if (value != null && _memberMap != null)
            {
                try
                {
                    var memberType = _memberMap.MemberType;

                    // if the member type closes IEnumerable<> then find the element type
                    if (typeof(IEnumerable<>).IsOpenTypeAssignableFrom(memberType))
                        memberType = memberType.GetInterfaceClosing(typeof(IEnumerable<>)).GetGenericArguments()[0];

                    // if the current type is not the MemberType and is IEnumerable, then it might be a $in query
                    if (memberType != value.GetType() && !(value is string) && value is IEnumerable)
                    {
                        value = ((IEnumerable)value).OfType<object>().Select(v => SerializeValue(v, _memberMap)).ToArray();
                    }
                    else
                    {
                        value = SerializeValue(value, _memberMap);
                    }
                }
                catch (Exception ex)
                {
                    throw new InvalidQueryException("An error occured when converting value with the serializer", ex);
                }
            }

            _value = BsonValue.Create(value) ?? BsonNull.Value;
        }

        private object SerializeValue(object value, BsonMemberMap memberMap)
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
    }
}