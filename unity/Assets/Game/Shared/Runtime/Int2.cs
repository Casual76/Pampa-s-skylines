#nullable enable

namespace PampaSkylines.Shared
{
using System;

public readonly struct Int2 : IEquatable<Int2>
{
    public Int2(int x, int y)
    {
        X = x;
        Y = y;
    }

    public int X { get; }

    public int Y { get; }

    public int ManhattanDistance(Int2 other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);

    public double EuclideanDistance(Int2 other)
    {
        var dx = X - other.X;
        var dy = Y - other.Y;
        return Math.Sqrt((dx * dx) + (dy * dy));
    }

    public bool Equals(Int2 other) => X == other.X && Y == other.Y;

    public override bool Equals(object? obj) => obj is Int2 other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public override string ToString() => $"{X},{Y}";
}
}
