using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using FluentMongo.Linq;
using FluentMongo.Context.Tracking;
using MongoDB.Driver;
using System.Linq.Expressions;
using FluentMongo.Linq.Util;
using System.Reflection;

namespace FluentMongo.Context.Tracking
{
    internal class TrackingQueryProvider : MongoQueryProvider
    {
        private readonly IChangeTracker _changeTracker;

        public TrackingQueryProvider(MongoCollection collection, IChangeTracker changeTracker)
            : base(collection)
        {
            _changeTracker = changeTracker;
        }

        /// <summary>
        /// Creates the query.
        /// </summary>
        /// <typeparam name="TElement">The type of the element.</typeparam>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public override IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new TrackingQuery<TElement>(base.CreateQuery<TElement>(expression), _changeTracker);
        }

        /// <summary>
        /// Constructs an <see cref="T:System.Linq.IQueryable"/> object that can evaluate the query represented by a specified expression tree.
        /// </summary>
        /// <param name="expression">An expression tree that represents a LINQ query.</param>
        /// <returns>
        /// An <see cref="T:System.Linq.IQueryable"/> that can evaluate the query represented by the specified expression tree.
        /// </returns>
        public override IQueryable CreateQuery(Expression expression)
        {
            Type elementType = TypeHelper.GetElementType(expression.Type);
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(TrackingQuery<>).MakeGenericType(elementType), new object[] { base.CreateQuery(expression), _changeTracker });
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }
    }
}
