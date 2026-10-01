namespace Kuwata.Core.Geometry;

/// <summary>Треугольная сетка: вершины и тройки индексов.</summary>
public sealed class Mesh
{
    public List<Vec3> Vertices { get; } = new();
    public List<int> Indices { get; } = new();

    public int TriangleCount => Indices.Count / 3;

    public int AddVertex(Vec3 v)
    {
        Vertices.Add(v);
        return Vertices.Count - 1;
    }

    public void AddTriangle(int a, int b, int c)
    {
        Indices.Add(a);
        Indices.Add(b);
        Indices.Add(c);
    }

    public (Vec3 A, Vec3 B, Vec3 C) Triangle(int i) =>
        (Vertices[Indices[3 * i]], Vertices[Indices[3 * i + 1]], Vertices[Indices[3 * i + 2]]);

    public Mesh Transformed(RigidTransform t)
    {
        var m = new Mesh();
        m.Vertices.AddRange(Vertices.Select(t.Apply));
        m.Indices.AddRange(Indices);
        return m;
    }

    public void Append(Mesh other)
    {
        int offset = Vertices.Count;
        Vertices.AddRange(other.Vertices);
        Indices.AddRange(other.Indices.Select(i => i + offset));
    }

    public (Vec3 Min, Vec3 Max) Bounds()
    {
        if (Vertices.Count == 0) throw new InvalidOperationException("Пустая сетка.");
        double minX = double.MaxValue, minY = double.MaxValue, minZ = double.MaxValue;
        double maxX = double.MinValue, maxY = double.MinValue, maxZ = double.MinValue;
        foreach (var v in Vertices)
        {
            minX = Math.Min(minX, v.X); minY = Math.Min(minY, v.Y); minZ = Math.Min(minZ, v.Z);
            maxX = Math.Max(maxX, v.X); maxY = Math.Max(maxY, v.Y); maxZ = Math.Max(maxZ, v.Z);
        }
        return (new Vec3(minX, minY, minZ), new Vec3(maxX, maxY, maxZ));
    }

    /// <summary>Добавляет обратную сторону (для открытых поверхностей, видимых с обеих сторон).</summary>
    public Mesh DoubleSided()
    {
        var m = new Mesh();
        m.Vertices.AddRange(Vertices);
        m.Indices.AddRange(Indices);
        for (int i = 0; i < Indices.Count; i += 3)
            m.AddTriangle(Indices[i], Indices[i + 2], Indices[i + 1]);
        return m;
    }
}
