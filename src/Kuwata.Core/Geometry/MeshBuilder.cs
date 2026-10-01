namespace Kuwata.Core.Geometry;

/// <summary>Простые примитивы для визуализации и экспорта (трубки вдоль кривых, сферы-маркеры).</summary>
public static class MeshBuilder
{
    /// <summary>Трубка радиуса r вдоль ломаной.</summary>
    public static Mesh Tube(IReadOnlyList<Vec3> path, double r, int sides = 8)
    {
        var m = new Mesh();
        if (path.Count < 2) return m;
        Vec3 prevNormal = Vec3.Zero;
        for (int i = 0; i < path.Count; i++)
        {
            var dir = (i == path.Count - 1 ? path[i] - path[i - 1] : path[i + 1] - path[i]).Normalized();
            // Устойчивая нормаль: переносим предыдущую, иначе берём любую перпендикулярную.
            Vec3 n = prevNormal == Vec3.Zero
                ? (Math.Abs(dir.Z) < 0.9 ? dir.Cross(Vec3.UnitZ) : dir.Cross(Vec3.UnitX)).Normalized()
                : (prevNormal - dir * prevNormal.Dot(dir)).Normalized();
            prevNormal = n;
            var b = dir.Cross(n);
            for (int s = 0; s < sides; s++)
            {
                double a = 2 * Math.PI * s / sides;
                m.AddVertex(path[i] + (n * Math.Cos(a) + b * Math.Sin(a)) * r);
            }
        }
        for (int i = 0; i < path.Count - 1; i++)
            for (int s = 0; s < sides; s++)
            {
                int a0 = i * sides + s, a1 = i * sides + (s + 1) % sides;
                int b0 = a0 + sides, b1 = a1 + sides;
                m.AddTriangle(a0, a1, b1);
                m.AddTriangle(a0, b1, b0);
            }
        return m;
    }

    public static Mesh Sphere(Vec3 c, double r, int slices = 12, int stacks = 8)
    {
        var m = new Mesh();
        for (int i = 0; i <= stacks; i++)
        {
            double phi = Math.PI * i / stacks;
            for (int j = 0; j < slices; j++)
            {
                double th = 2 * Math.PI * j / slices;
                m.AddVertex(c + new Vec3(Math.Sin(phi) * Math.Cos(th), Math.Sin(phi) * Math.Sin(th), Math.Cos(phi)) * r);
            }
        }
        for (int i = 0; i < stacks; i++)
            for (int j = 0; j < slices; j++)
            {
                int a = i * slices + j, b = i * slices + (j + 1) % slices;
                int c2 = a + slices, d = b + slices;
                m.AddTriangle(a, c2, b);
                m.AddTriangle(b, c2, d);
            }
        return m;
    }
}
