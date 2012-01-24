using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MongoDB.Driver;
using MongoDB.Bson;
using MongoDB.Driver.Builders;

namespace FluentMongo.Context.Tracking
{
    public class UpdateDocumentBuilder
    {
        private readonly BsonDocument _original;
        private readonly BsonDocument _current;

        private List<MatchedElement> _matchedElements;
        private List<BsonElement> _sets;
        private List<BsonElement> _unsets;
        private UpdateDocument _updateDoc;

        public UpdateDocumentBuilder(BsonDocument original, BsonDocument current)
        {
            _sets = new List<BsonElement>();
            _unsets = new List<BsonElement>();

            _original = original;
            _current = current;
        }

        public UpdateDocument Build()
        {
            MatchElements();
            CalculateAdditions();
            CalculateRemovals();
            CalculateChanges();

            _updateDoc = new UpdateDocument();

            if(_unsets.Count > 0)
                _updateDoc.Add("$unset", new BsonDocument(_unsets));
            if(_sets.Count > 0)
                _updateDoc.Add("$set", new BsonDocument(_sets));

            return _updateDoc;
        }

        private void CalculateChanges()
        {
            var possibleChanges = _matchedElements.Where(x => x.Original != null && x.Current != null);
            foreach (var possibleChange in possibleChanges)
            {
                if (possibleChange.Original.Value.Equals(possibleChange.Current.Value))
                    continue;

                if (possibleChange.Original.Value.IsBsonDocument && possibleChange.Current.Value.IsBsonDocument)
                {
                    var subUpdateDocument = new UpdateDocumentBuilder(possibleChange.Original.Value.AsBsonDocument, possibleChange.Current.Value.AsBsonDocument).Build();
                    var localSets = new List<BsonElement>();
                    var localUnsets = new List<BsonElement>();
                    if(subUpdateDocument.Contains("$set"))
                        localSets.AddRange(subUpdateDocument["$set"].AsBsonDocument.Elements);
                    if(subUpdateDocument.Contains("$unset"))
                        localUnsets.AddRange(subUpdateDocument["$unset"].AsBsonDocument.Elements);

                    //test to see if all elements have been accounted for, and therefore we'll simply use the entire current document.
                    var originalNames = possibleChange.Original.Value.AsBsonDocument.Elements.Select(x => x.Name);
                    var updatedNames = localSets.Select(x => x.Name).Union(localUnsets.Select(x => x.Name));
                    var difference = originalNames.Except(updatedNames);
                    if (!difference.Any())
                        _sets.Add(possibleChange.Current);
                    else
                    {
                        _sets.AddRange(localSets.Select(x => new BsonElement(possibleChange.Current.Name + "." + x.Name, x.Value)));
                        _unsets.AddRange(localUnsets.Select(x => new BsonElement(possibleChange.Current.Name + "." + x.Name, x.Value)));
                    }
                }
                else
                {
                    _sets.Add(possibleChange.Current);
                }
            }
        }

        private void CalculateAdditions()
        {
            var elementsToSet = _matchedElements.Where(x => x.Original == null);

            _sets.AddRange(elementsToSet.Select(x => x.Current));
        }

        private void CalculateRemovals()
        {
            var elementsToUnset = _matchedElements.Where(x => x.Current == null);

            _unsets.AddRange(elementsToUnset.Select(x => new BsonElement(x.Original.Name, 1)));
        }

        private void MatchElements()
        {
            var leftJoin = from o in _original.Elements
                           join c in _current.Elements on o.Name equals c.Name into temp
                           from dm in temp.DefaultIfEmpty()
                           select new { Original = o, Current = dm };

            var rightJoin = from c in _current.Elements
                            join o in _original.Elements on c.Name equals o.Name into temp
                            from dm in temp.DefaultIfEmpty()
                            select new { Original = dm, Current = c };

            _matchedElements = leftJoin.Union(rightJoin).Select(x => new MatchedElement { Original = x.Original, Current = x.Current }).ToList();
        }

        private BsonElement AddParentElementNameIfNecessary(string parentName, BsonElement element)
        {
            if (parentName == null)
                return element;

            return new BsonElement(parentName + "." + element.Name, element.Value);
        }

        private class MatchedElement
        {
            public BsonElement Original { get; set; }

            public BsonElement Current { get; set; }
        }
    }
}
