using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using FluentMongo.Linq.Expressions;
using FluentMongo.Linq.Translators;
using System.Collections;

using MongoDB.Driver;
using MongoDB.Bson;
using FluentMongo.Linq.Util;
using MongoDB.Driver.Builders;

namespace FluentMongo.Linq
{
    /// <summary>
    /// 
    /// </summary>
    internal class MongoQueryProvider : IQueryProvider
    {
        private readonly MongoCollection _collection;

        /// <summary>
        /// Gets the name of the collection.
        /// </summary>
        /// <value>The name of the collection.</value>
        public MongoCollection Collection
        {
            get { return _collection; }
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="MongoQueryProvider"/> class.
        /// </summary>
        /// <param name="database">The database.</param>
        /// <param name="collectionName">Name of the collection.</param>
        public MongoQueryProvider(MongoCollection collection)
        {
            if (collection == null)
                throw new ArgumentNullException("collection");

            _collection = collection;
        }

        /// <summary>
        /// Creates the query.
        /// </summary>
        /// <typeparam name="TElement">The type of the element.</typeparam>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public virtual IQueryable<TElement> CreateQuery<TElement>(Expression expression)
        {
            return new MongoQuery<TElement>(this, expression);
        }

        /// <summary>
        /// Constructs an <see cref="T:System.Linq.IQueryable"/> object that can evaluate the query represented by a specified expression tree.
        /// </summary>
        /// <param name="expression">An expression tree that represents a LINQ query.</param>
        /// <returns>
        /// An <see cref="T:System.Linq.IQueryable"/> that can evaluate the query represented by the specified expression tree.
        /// </returns>
        public virtual IQueryable CreateQuery(Expression expression)
        {
            Type elementType = TypeHelper.GetElementType(expression.Type);
            try
            {
                return (IQueryable)Activator.CreateInstance(typeof(MongoQuery<>).MakeGenericType(elementType), new object[] { this, expression });
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException;
            }
        }

        /// <summary>
        /// Executes the specified expression.
        /// </summary>
        /// <typeparam name="TResult">The type of the result.</typeparam>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        public TResult Execute<TResult>(Expression expression)
        {
            object result = Execute(expression);
            return (TResult)result;
        }

        /// <summary>
        /// Executes the query represented by a specified expression tree.
        /// </summary>
        /// <param name="expression">An expression tree that represents a LINQ query.</param>
        /// <returns>
        /// The value that results from executing the specified query.
        /// </returns>
        public object Execute(Expression expression)
        {
            var plan = BuildExecutionPlan(expression);

            var lambda = expression as LambdaExpression;
            if (lambda != null)
            {
                var fn = Expression.Lambda(lambda.Type, plan, lambda.Parameters);
                return fn.Compile();
            }
            else
            {
                var efn = Expression.Lambda<Func<object>>(Expression.Convert(plan, typeof(object)));
                var fn = efn.Compile();
                return fn();
            }
        }

        /// <summary>
        /// Gets the query object.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns></returns>
        internal MongoQueryObject GetQueryObject(Expression expression)
        {
            var projection = Translate(expression);
            return new MongoQueryObjectBuilder().Build(projection);
        }

        /// <summary>
        /// Executes the query object.
        /// </summary>
        /// <param name="queryObject">The query object.</param>
        /// <returns></returns>
        internal object ExecuteQueryObject(MongoQueryObject queryObject)
        {
            if (queryObject.IsCount)
                return ExecuteCount(queryObject);
            if (queryObject.IsMapReduce)
                return ExecuteMapReduce(queryObject);
            return ExecuteFind(queryObject);
        }

        private Expression BuildExecutionPlan(Expression expression)
        {
            var lambda = expression as LambdaExpression;
            if (lambda != null)
                expression = lambda.Body;

            var projection = Translate(expression);

            var rootQueryable = new RootQueryableFinder().Find(expression);
            var provider = Expression.Convert(
                Expression.Property(rootQueryable, typeof(IQueryable).GetProperty("Provider")),
                typeof(MongoQueryProvider));

            return new ExecutionBuilder().Build(projection, provider);
        }

        private Expression Translate(Expression expression)
        {
            var rootQueryable = new RootQueryableFinder().Find(expression);
            var elementType = ((IQueryable)((ConstantExpression)rootQueryable).Value).ElementType;

            expression = PartialEvaluator.Evaluate(expression, CanBeEvaluatedLocally);

            expression = new FieldBinder().Bind(expression, elementType);
            expression = new QueryBinder(this, expression).Bind(expression);
            expression = new AggregateRewriter().Rewrite(expression);
            expression = new RedundantFieldRemover().Remove(expression);
            expression = new RedundantSubqueryRemover().Remove(expression);

            expression = new OrderByRewriter().Rewrite(expression);
            expression = new RedundantFieldRemover().Remove(expression);
            expression = new RedundantSubqueryRemover().Remove(expression);

            return expression;
        }

        /// <summary>
        /// Determines whether this instance [can be evaluated locally] the specified expression.
        /// </summary>
        /// <param name="expression">The expression.</param>
        /// <returns>
        /// 	<c>true</c> if this instance [can be evaluated locally] the specified expression; otherwise, <c>false</c>.
        /// </returns>
        private bool CanBeEvaluatedLocally(Expression expression)
        {
            // any operation on a query can't be done locally
            ConstantExpression cex = expression as ConstantExpression;
            if (cex != null)
            {
                IQueryable query = cex.Value as IQueryable;
                if (query != null && query.Provider == this)
                    return false;
            }
            MethodCallExpression mc = expression as MethodCallExpression;
            if (mc != null && (mc.Method.DeclaringType == typeof(Enumerable) || mc.Method.DeclaringType == typeof(Queryable) || mc.Method.DeclaringType == typeof(MongoQueryable)))
            {
                return false;
            }
            if (expression.NodeType == ExpressionType.Convert &&
                expression.Type == typeof(object))
                return true;
            return expression.NodeType != ExpressionType.Parameter &&
                   expression.NodeType != ExpressionType.Lambda;
        }

        /// <summary>
        /// Executes the count.
        /// </summary>
        /// <param name="queryObject">The query object.</param>
        /// <returns></returns>
        private object ExecuteCount(MongoQueryObject queryObject)
        {
            if (queryObject.Query == null)
                return (int)_collection.Count();

            return (int)_collection.Count(new QueryDocument(queryObject.Query));
        }

        private object ExecuteFind(MongoQueryObject queryObject)
        {
            var findAsMethod = typeof(MongoCollection).GetGenericMethod(
                "FindAs",
                BindingFlags.Public | BindingFlags.Instance,
                new[] { queryObject.DocumentType },
                new[] { typeof(IMongoQuery) });

            QueryDocument queryDocument;
            if (queryObject.Sort != null)
            {
                queryDocument = new QueryDocument
                {
                    {"query", queryObject.Query}, 
                    {"orderby", queryObject.Sort}
                };
            }
            else
                queryDocument = new QueryDocument(queryObject.Query);

            var cursor = findAsMethod.Invoke(_collection, new[] { queryDocument });
            var cursorType = cursor.GetType();
            if (queryObject.Fields.ElementCount > 0)
            {
                var setFieldsMethod = cursorType.GetMethod(
                    "SetFields",
                   BindingFlags.Public | BindingFlags.Instance,
                    null,
                    new[] { typeof(IMongoFields) },
                    null
                );

                setFieldsMethod.Invoke(cursor, new[] { new FieldsDocument(queryObject.Fields) });
            }

            var setLimitMethod = cursorType.GetMethod(
                "SetLimit",
                 BindingFlags.Public | BindingFlags.Instance,
                 null,
                 new[] { typeof(int) },
                 null);

            setLimitMethod.Invoke(cursor, new object[] { queryObject.NumberToLimit });

            var setSkipMethod = cursorType.GetMethod(
                "SetSkip",
                 BindingFlags.Public | BindingFlags.Instance,
                 null,
                 new[] { typeof(int) },
                 null);

            setSkipMethod.Invoke(cursor, new object[] { queryObject.NumberToSkip });

            var executor = GetExecutor(queryObject.DocumentType, queryObject.Projector, queryObject.Aggregator, true);
            return executor.Compile().DynamicInvoke(cursor);
        }

        private object ExecuteMapReduce(MongoQueryObject queryObject)
        {
            var options = new MapReduceOptionsBuilder();

            options.SetQuery(new QueryDocument(queryObject.Query));
            options.SetFinalize(new BsonJavaScript(queryObject.FinalizerFunction));
            options.SetLimit(queryObject.NumberToLimit);
            options.SetOutput(MapReduceOutput.Inline);

            if (queryObject.Sort != null)
                options.SetSortOrder(new SortByDocument(queryObject.Sort));

            if (queryObject.NumberToSkip != 0)
                throw new InvalidQueryException("MapReduce queries do not support Skips.");

            var mapReduce = _collection.MapReduce(
                new BsonJavaScript(queryObject.MapFunction),
                new BsonJavaScript(queryObject.ReduceFunction),
                options);

            var executor = GetExecutor(typeof(BsonDocument), queryObject.Projector, queryObject.Aggregator, true);
            return executor.Compile().DynamicInvoke(mapReduce.InlineResults);
        }

        private static LambdaExpression GetExecutor(Type documentType, LambdaExpression projector,
            Expression aggregator, bool boxReturn)
        {
            var documents = Expression.Parameter(typeof(IEnumerable), "documents");
            Expression body = Expression.Call(
                typeof(MongoQueryProvider),
                "Project",
                new[] { documentType, projector.Body.Type },
                documents,
                projector);
            if (aggregator != null)
                body = Expression.Invoke(aggregator, body);

            if (boxReturn && body.Type != typeof(object))
                body = Expression.Convert(body, typeof(object));

            return Expression.Lambda(body, documents);
        }

        private static IEnumerable<TResult> Project<TDocument, TResult>(IEnumerable documents, Func<TDocument, TResult> projector)
        {
            return documents.OfType<TDocument>().Select(projector);
        }

        private class RootQueryableFinder : MongoExpressionVisitor
        {
            private Expression _root;

            public Expression Find(Expression expression)
            {
                Visit(expression);
                return _root;
            }

            protected override Expression Visit(Expression exp)
            {
                Expression result = base.Visit(exp);

                if (this._root == null && result != null && typeof(IQueryable).IsAssignableFrom(result.Type))
                {
                    this._root = result;
                }

                return result;
            }
        }
    }
}