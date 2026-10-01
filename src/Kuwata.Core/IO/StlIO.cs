using System.Globalization;
using System.Text;
using Kuwata.Core.Geometry;

namespace Kuwata.Core.IO;

/// <summary>Чтение STL (бинарный и ASCII) и запись бинарного STL. Совпадающие вершины сливаются.</summary>
public static class StlIO
{
    public static Mesh Read(string path)
    {
        using var fs = File.OpenRead(path);
        return Read(fs);
    }

    public static Mesh Read(Stream stream)
    {
        using var ms = new MemoryStream();
        stream.CopyTo(ms);
        var data = ms.ToArray();
        return IsBinary(data) ? ReadBinary(data) : ReadAscii(Encoding.ASCII.GetString(data));
    }

    // Бинарный STL может начинаться со слова "solid", поэтому проверяем по размеру.
    private static bool IsBinary(byte[] data)
    {
        if (data.Length < 84) return false;
        uint n = BitConverter.ToUInt32(data, 80);
        return 84 + 50L * n == data.Length;
    }

    private static Mesh ReadBinary(byte[] data)
    {
        uint n = BitConverter.ToUInt32(data, 80);
        var welder = new VertexWelder();
        int off = 84;
        for (uint i = 0; i < n; i++)
        {
            off += 12; // нормаль пересчитывается при необходимости
            int[] idx = new int[3];
            for (int k = 0; k < 3; k++)
            {
                var v = new Vec3(BitConverter.ToSingle(data, off), BitConverter.ToSingle(data, off + 4), BitConverter.ToSingle(data, off + 8));
                idx[k] = welder.Add(v);
                off += 12;
            }
            off += 2;
            welder.Mesh.AddTriangle(idx[0], idx[1], idx[2]);
        }
        return welder.Mesh;
    }

    private static Mesh ReadAscii(string text)
    {
        var welder = new VertexWelder();
        var tri = new List<int>(3);
        foreach (var raw in text.Split('\n'))
        {
            var line = raw.Trim();
            if (!line.StartsWith("vertex", StringComparison.OrdinalIgnoreCase)) continue;
            var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
            var v = new Vec3(
                double.Parse(p[1], CultureInfo.InvariantCulture),
                double.Parse(p[2], CultureInfo.InvariantCulture),
                double.Parse(p[3], CultureInfo.InvariantCulture));
            tri.Add(welder.Add(v));
            if (tri.Count == 3)
            {
                welder.Mesh.AddTriangle(tri[0], tri[1], tri[2]);
                tri.Clear();
            }
        }
        if (welder.Mesh.TriangleCount == 0) throw new InvalidDataException("STL не содержит треугольников.");
        return welder.Mesh;
    }

    public static void WriteBinary(string path, Mesh mesh, string header = "Kuwata occlusion")
    {
        using var fs = File.Create(path);
        WriteBinary(fs, mesh, header);
    }

    public static void WriteBinary(Stream stream, Mesh mesh, string header = "Kuwata occlusion")
    {
        using var w = new BinaryWriter(stream, Encoding.ASCII, leaveOpen: true);
        var h = new byte[80];
        Encoding.ASCII.GetBytes(header, 0, Math.Min(header.Length, 80), h, 0);
        w.Write(h);
        w.Write((uint)mesh.TriangleCount);
        for (int i = 0; i < mesh.TriangleCount; i++)
        {
            var (a, b, c) = mesh.Triangle(i);
            var n = (b - a).Cross(c - a);
            n = n.Length > 1e-12 ? n.Normalized() : Vec3.Zero;
            foreach (var v in new[] { n, a, b, c })
            {
                w.Write((float)v.X);
                w.Write((float)v.Y);
                w.Write((float)v.Z);
            }
            w.Write((ushort)0);
        }
    }

    private sealed class VertexWelder
    {
        public Mesh Mesh { get; } = new();
        private readonly Dictionary<(float, float, float), int> _map = new();

        public int Add(Vec3 v)
        {
            var key = ((float)v.X, (float)v.Y, (float)v.Z);
            if (_map.TryGetValue(key, out int i)) return i;
            i = Mesh.AddVertex(v);
            _map[key] = i;
            return i;
        }
    }
}
