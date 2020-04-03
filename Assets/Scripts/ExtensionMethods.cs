using System;
using System.Collections.Generic;
using System.Linq;

public static class ExtensionMethods
{
    private static Random rng = new Random();

    public static IList<T> Shuffle<T>(this IList<T> l)
    {
        IList<T> list = l;
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
        return list;
    }
}
