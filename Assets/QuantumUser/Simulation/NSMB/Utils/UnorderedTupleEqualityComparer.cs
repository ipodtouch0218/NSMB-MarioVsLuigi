using System.Collections.Generic;
using System;

// https://stackoverflow.com/a/74052588/19635374
public class UnorderedTupleEqualityComparer<T> : IEqualityComparer<(T, T)> {
    private readonly IEqualityComparer<T> _comparer;

    public UnorderedTupleEqualityComparer(IEqualityComparer<T> comparer = default) {
        _comparer = comparer ?? EqualityComparer<T>.Default;
    }

    public bool Equals((T, T) x, (T, T) y) {
        if (_comparer.Equals(x.Item1, y.Item1)
            && _comparer.Equals(x.Item2, y.Item2)) return true;
        if (_comparer.Equals(x.Item1, y.Item2)
            && _comparer.Equals(x.Item2, y.Item1)) return true;
        return false;
    }

    public int GetHashCode((T, T) obj) {
        int h1 = _comparer.GetHashCode(obj.Item1);
        int h2 = _comparer.GetHashCode(obj.Item2);
        if (h1 > h2) (h1, h2) = (h2, h1);
        return HashCode.Combine(h1, h2);
    }
}