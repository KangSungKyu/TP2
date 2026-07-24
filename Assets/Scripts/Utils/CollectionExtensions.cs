using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public static class CollectionExtensions
{
    public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> ie)
    {
        List<T> li = ie.ToList();
        int n = li.Count;

        while (n > 1)
        {
            n -= 1;

            int k = UnityEngine.Random.Range(0, n + 1);
            T v = li[k];
            li[k] = li[n];
            li[n] = v;
        }

        foreach(T item in li)
        {
            yield return item;
        }
    }
}