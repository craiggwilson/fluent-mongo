using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq.Expressions;
using FluentMongo.Linq.Expressions;

namespace FluentMongo.Linq.Translators
{
    internal class FieldGatherer : MongoExpressionVisitor
    {
        private List<FieldExpression> _fields;
        
        public ReadOnlyCollection<FieldExpression> Gather(Expression exp)
        {
            _fields = new List<FieldExpression>();
            Visit(exp);
            return _fields.AsReadOnly();
        }

        protected override Expression VisitSelect(SelectExpression select)
        {
            VisitFieldDeclarationList(select.Fields);
            return select;
        }

        protected override Expression VisitField(FieldExpression field)
        {
            var fields = new FieldGatherer().Gather(field.Expression);
            if (fields.Count == 0)
                _fields.Add(field);
            else
                _fields.AddRange(fields);

            return base.VisitField(field);
        }
    }
}
