using System;
using System.Collections.Generic;

public static class LinqExtensions {
    // Credit: https://stackoverflow.com/a/29971633/19635374
    public static int IndexOf<TSource>(this IEnumerable<TSource> source, Func<TSource, bool> predicate) {

        var index = 0;
        foreach (var item in source) {
            if (predicate.Invoke(item)) {
                return index;
            }
            index++;
        }

        return -1;
    }
}