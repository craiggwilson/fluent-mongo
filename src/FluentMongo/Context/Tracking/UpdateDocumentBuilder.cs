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
        private List<BsonElement> _pops;
        private UpdateDocument _updateDoc;

        public UpdateDocumentBuilder(BsonDocument original, BsonDocument current)
        {
            _sets = new List<BsonElement>();
            _unsets = new List<BsonElement>();
            _pops = new List<BsonElement>();

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
            if (_pops.Count > 0)
                _updateDoc.Add("$pop", new BsonDocument(_pops));

            return _updateDoc;
        }

        private void CalculateChanges()
        {
            var possibleChanges = _matchedElements.Where(x => x.Original != null && x.Current != null);
            foreach (var possibleChange in possibleChanges)
            {
                if (possibleChange.Original.Equals(possibleChange.Current))
                    continue;

                if (possibleChange.Original.IsBsonDocument && possibleChange.Current.IsBsonDocument)
                {
                    CalculateChangesForNestedDocument(possibleChange.Name, possibleChange.Original.AsBsonDocument, possibleChange.Current.AsBsonDocument);
                }
                else if (possibleChange.Original.IsBsonArray && possibleChange.Current.IsBsonArray)
                {
                    CalculateChangesForArray(possibleChange.Name, possibleChange.Original.AsBsonArray, possibleChange.Current.AsBsonArray);
                }
                else
                {
                    _sets.Add(new BsonElement(possibleChange.Name, possibleChange.Current));
                }
            }
        }

        private void CalculateChangesForNestedDocument(string elementName, BsonDocument original, BsonDocument current)
        {
            var subUpdateDocument = new UpdateDocumentBuilder(original, current).Build();
            var localSets = new List<BsonElement>();
            var localUnsets = new List<BsonElement>();
            if (subUpdateDocument.Contains("$set"))
                localSets.AddRange(subUpdateDocument["$set"].AsBsonDocument.Elements);
            if (subUpdateDocument.Contains("$unset"))
                localUnsets.AddRange(subUpdateDocument["$unset"].AsBsonDocument.Elements);

            //test to see if all elements have been accounted for, and therefore we'll simply use the entire current document.
            var originalNames = original.Elements.Select(x => x.Name);
            var updatedNames = localSets.Select(x => x.Name).Union(localUnsets.Select(x => x.Name));
            var difference = originalNames.Except(updatedNames);
            if (!difference.Any())
                _sets.Add(new BsonElement(elementName, current));
            else
            {
                _sets.AddRange(localSets.Select(x => new BsonElement(elementName + "." + x.Name, x.Value)));
                _unsets.AddRange(localUnsets.Select(x => new BsonElement(elementName + "." + x.Name, x.Value)));
            }
        }

        private void CalculateChangesForArray(string elementName, BsonArray original, BsonArray current)
        {
            //there is no atomic way of popping more than one element off the back of an array, so if current.Count < original.Count+ 1, we simply have to replace the whole thing
            if(original.Count == 0 || current.Count == 0 || current.Count + 2 <= original.Count)
            {
                _sets.Add(new BsonElement(elementName, current));
                return;
            }

            int i = 0;
            for (; i < original.Count && i < current.Count; i++)
            {
                if (original[i] == current[i])
                    continue;

                if (original[i].IsBsonDocument && current[i].IsBsonDocument)
                {
                    CalculateChangesForNestedDocument(elementName + "." + i, original[i].AsBsonDocument, current[i].AsBsonDocument);
                }
                else if (original[i].IsBsonArray && current[i].IsBsonArray)
                {
                    CalculateChangesForArray(elementName + "." + i, original[i].AsBsonArray, current[i].AsBsonArray);
                }
                else
                {
                    _sets.Add(new BsonElement(elementName + "." + i, current[i]));
                }
            }

            int currentIndex = i;
            for(;i < current.Count; i++)
            {
                _sets.Add(new BsonElement(elementName + "." + i, current[i]));
            }
            if (i < original.Count) //because of the condition at the top, there will only be one extra at the end...
            {
                _pops.Add(new BsonElement(elementName, 1));
            }
        }

        private void CalculateAdditions()
        {
            var elementsToSet = _matchedElements.Where(x => x.Original == null);

            _sets.AddRange(elementsToSet.Select(x => new BsonElement(x.Name, x.Current)));
        }

        private void CalculateRemovals()
        {
            var elementsToUnset = _matchedElements.Where(x => x.Current == null);

            _unsets.AddRange(elementsToUnset.Select(x => new BsonElement(x.Name, 1)));
        }

        private void MatchElements()
        {
            var leftJoin = from o in _original.Elements
                           join c in _current.Elements on o.Name equals c.Name into temp
                           from dm in temp.DefaultIfEmpty()
                           select new { Name = o.Name, Original = o, Current = dm };

            var rightJoin = from c in _current.Elements
                            join o in _original.Elements on c.Name equals o.Name into temp
                            from dm in temp.DefaultIfEmpty()
                            select new { Name = c.Name, Original = dm, Current = c };

            _matchedElements = leftJoin.Union(rightJoin).Select(x => new MatchedElement { Name = x.Name, Original = x.Original == null ? null : x.Original.Value, Current = x.Current == null ? null : x.Current.Value }).ToList();
        }

        private class MatchedElement
        {
            public string Name { get; set; }

            public BsonValue Original { get; set; }

            public BsonValue Current { get; set; }
        }
    }
}
