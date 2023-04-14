using System;
using System.Collections.Generic;
using System.Linq;

namespace MASLib
{
    internal static class Ut
    {
        //Copied from the modkit since I'm using an external assembly
        internal static T PickRandom<T>(this IEnumerable<T> src)
        {
            var list = (src as IList<T>) ?? src.ToArray();
            if (list.Count == 0)
            {
                throw new InvalidOperationException("Cannot pick an element from an empty set.");
            }
                
            return list[UnityEngine.Random.Range(0, list.Count)];
        }
    }
}