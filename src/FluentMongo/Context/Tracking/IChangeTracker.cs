using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace FluentMongo.Context.Tracking
{
    internal interface IChangeTracker : IDisposable
    {
        ITrackedObject GetTrackedObject(object obj);

        ITrackedObject Track(object obj);
    }
}