namespace Kuwata.Core.Geometry;

/// <summary>
/// Пространственная сетка треугольников для быстрого поиска знакового расстояния
/// от точки до поверхности. Знак — по нормали ближайшего треугольника
/// (положительно — снаружи поверхности, отрицательно — внутри / проникновение).
/// </summary>
public sealed class ProximityGrid
{
    private readonly Mesh _mesh;
    private readonly double _cell;
    private readonly Dictionary<(int, int, int), List<int>> _cells = new();
    private readonly Vec3[] _normals;

    public ProximityGrid(Mesh mesh, double cellSize = 1.0)
    {
        _mesh = mesh;
        _cell = cellSize;
        _normals = new Vec3[mesh.TriangleCount];
        for (int t = 0; t < mesh.TriangleCount; t++)
        {
            var (a, b, c) = mesh.Triangle(t);
            var n = (b - a).Cross(c - a);
            _normals[t] = n.Length > 1e-15 ? n.Normalized() : Vec3.Zero;
            var lo = Key(new Vec3(Math.Min(a.X, Math.Min(b.X, c.X)), Math.Min(a.Y, Math.Min(b.Y, c.Y)), Math.Min(a.Z, Math.Min(b.Z, c.Z))));
            var hi = Key(new Vec3(Math.Max(a.X, Math.Max(b.X, c.X)), Math.Max(a.Y, Math.Max(b.Y, c.Y)), Math.Max(a.Z, Math.Max(b.Z, c.Z))));
            for (int i = lo.Item1; i <= hi.Item1; i++)
                for (int j = lo.Item2; j <= hi.Item2; j++)
                    for (int k = lo.Item3; k <= hi.Item3; k++)
                    {
                        if (!_cells.TryGetValue((i, j, k), out var list)) _cells[(i, j, k)] = list = new List<int>();
                        list.Add(t);
                    }
        }
    }

    private (int, int, int) Key(Vec3 p) =>
        ((int)Math.Floor(p.X / _cell), (int)Math.Floor(p.Y / _cell), (int)Math.Floor(p.Z / _cell));

    /// <summary>Знаковое расстояние до ближайшего треугольника в радиусе maxDist; null — дальше.</summary>
    public double? SignedDistance(Vec3 p, double maxDist)
    {
        var lo = Key(p - new Vec3(maxDist, maxDist, maxDist));
        var hi = Key(p + new Vec3(maxDist, maxDist, maxDist));
        double best = double.MaxValue;
        int bestT = -1;
        Vec3 bestQ = default;
        for (int i = lo.Item1; i <= hi.Item1; i++)
            for (int j = lo.Item2; j <= hi.Item2; j++)
                for (int k = lo.Item3; k <= hi.Item3; k++)
                {
                    if (!_cells.TryGetValue((i, j, k), out var list)) continue;
                    foreach (int t in list)
                    {
                        var (a, b, c) = _mesh.Triangle(t);
                        var q = ClosestPointOnTriangle(p, a, b, c);
                        double d = (p - q).Length;
                        if (d < best) { best = d; bestT = t; bestQ = q; }
                    }
                }
        if (bestT < 0 || best > maxDist) return null;
        double side = (p - bestQ).Dot(_normals[bestT]);
        return side < 0 ? -best : best;
    }

    /// <summary>Ближайшая точка треугольника (Ericson, Real-Time Collision Detection, 5.1.5).</summary>
    public static Vec3 ClosestPointOnTriangle(Vec3 p, Vec3 a, Vec3 b, Vec3 c)
    {
        var ab = b - a; var ac = c - a; var ap = p - a;
        double d1 = ab.Dot(ap), d2 = ac.Dot(ap);
        if (d1 <= 0 && d2 <= 0) return a;
        var bp = p - b;
        double d3 = ab.Dot(bp), d4 = ac.Dot(bp);
        if (d3 >= 0 && d4 <= d3) return b;
        double vc = d1 * d4 - d3 * d2;
        if (vc <= 0 && d1 >= 0 && d3 <= 0) return a + ab * (d1 / (d1 - d3));
        var cp = p - c;
        double d5 = ab.Dot(cp), d6 = ac.Dot(cp);
        if (d6 >= 0 && d5 <= d6) return c;
        double vb = d5 * d2 - d1 * d6;
        if (vb <= 0 && d2 >= 0 && d6 <= 0) return a + ac * (d2 / (d2 - d6));
        double va = d3 * d6 - d5 * d4;
        if (va <= 0 && d4 - d3 >= 0 && d5 - d6 >= 0) return b + (c - b) * ((d4 - d3) / ((d4 - d3) + (d5 - d6)));
        double denom = 1 / (va + vb + vc);
        return a + ab * (vb * denom) + ac * (vc * denom);
    }
}
