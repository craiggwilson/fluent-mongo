using System;
using System.Collections;
using System.Globalization;
using System.Text;
using MongoDB.Bson;
using MongoDB.Driver;

namespace FluentMongo.Linq.Util
{
    /// <summary>
    ///   Lightweight routines to handle basic json serializing.
    /// </summary>
    internal static class JavasriptHelper
    {
        /// <summary>
        /// Serializes for server side.
        /// </summary>
        /// <param name="value">The value.</param>
        /// <returns></returns>
        public static string SerializeForServerSide(object value)
        {
            var sb = new StringBuilder();
            if (value is DateTime)
            {
                DateTime d = (DateTime)value;
                sb.AppendFormat("new Date({0},{1},{2},{3},{4},{5},{6})", d.Year, d.Month - 1, d.Day, d.Hour, d.Minute, d.Second, d.Millisecond);
            }
            else
                SerializeType(value, sb);
            return sb.ToString();
        }

        /// <summary>
        ///   Serializes the type.
        /// </summary>
        /// <param name = "value">The value.</param>
        /// <param name = "json">The json.</param>
        private static void SerializeType(object value, StringBuilder json)
        {
            json.Append(BsonValue.Create(value).ToJson());
        }

        /// <summary>
        ///   Escapes any characters that are special to javascript.
        /// </summary>
        public static string Escape(string text)
        {
            var builder = new StringBuilder();
            foreach(var c in text)
                switch(c)
                {
                    case '\b':
                        builder.Append(@"\b");
                        break;
                    case '\f':
                        builder.Append(@"\f");
                        break;
                    case '\n':
                        builder.Append(@"\n");
                        break;
                    case '\r':
                        builder.Append(@"\r");
                        break;
                    case '\t':
                        builder.Append(@"\t");
                        break;
                    case '\v':
                        builder.Append(@"\v");
                        break;
                    case '\'':
                        builder.Append(@"\'");
                        break;
                    case '"':
                        builder.Append("\\\"");
                        break;
                    case '\\':
                        builder.Append(@"\\");
                        break;
                    default:
                        if(c <= '\u001f')
                        {
                            builder.Append("\\u");
                            builder.Append(((int)c).ToString("x4"));
                        }
                        else
                            builder.Append(c);
                        break;
                }
            return builder.ToString();
        }
    }
}