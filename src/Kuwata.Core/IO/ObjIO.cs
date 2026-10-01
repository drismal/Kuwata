using System.Globalization;
using Kuwata.Core.Geometry;

namespace Kuwata.Core.IO;

/// <summary>Чтение OBJ: только вершины и грани (многоугольники разбиваются веером).</summary>
public static class ObjIO
{
    public static Mesh Read(string path) => Parse(File.ReadAllLines(path));

    public static Mesh Parse(IEnumerable<string> lines)
    {
        var m = new Mesh();
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.StartsWith("v ", StringComparison.Ordinal))
            {
                var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                m.AddVertex(new Vec3(
                    double.Parse(p[1], CultureInfo.InvariantCulture),
                    double.Parse(p[2], CultureInfo.InvariantCulture),
                    double.Parse(p[3], CultureInfo.InvariantCulture)));
            }
            else if (line.StartsWith("f ", StringComparison.Ordinal))
            {
                var p = line.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
                var idx = p.Skip(1).Select(tok =>
                {
                    int i = int.Parse(tok.Split('/')[0], CultureInfo.InvariantCulture);
                    return i > 0 ? i - 1 : m.Vertices.Count + i; // отрицательные индексы — от конца
                }).ToArray();
                for (int k = 1; k + 1 < idx.Length; k++) m.AddTriangle(idx[0], idx[k], idx[k + 1]);
            }
        }
        if (m.TriangleCount == 0) throw new InvalidDataException("OBJ не содержит граней.");
        return m;
    }
}

public static class MeshFiles
{
    /// <summary>Загрузка STL или OBJ по расширению.</summary>
    public static Mesh Load(string path) => Path.GetExtension(path).ToLowerInvariant() switch
    {
        ".stl" => StlIO.Read(path),
        ".obj" => ObjIO.Read(path),
        var ext => throw new NotSupportedException($"Формат {ext} не поддерживается (нужен STL или OBJ)."),
    };
}
