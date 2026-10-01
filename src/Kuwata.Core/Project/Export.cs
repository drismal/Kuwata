using System.Text.Json;
using Kuwata.Core.Geometry;
using Kuwata.Core.IO;

namespace Kuwata.Core.Project;

public enum ExportFrame
{
    /// <summary>Система артикулятора Artex CR.</summary>
    Articulator,
    /// <summary>Исходные координаты сканов — для exocad / 3Shape поверх исходных моделей.</summary>
    OriginalScans,
}

public static class Export
{
    /// <summary>
    /// Окклюзионная поверхность (двусторонняя), при желании — кривые трубками.
    /// </summary>
    public static Mesh SurfaceMesh(CaseResult r, ExportFrame frame, bool includeCurves, double tubeRadius = 0.3)
    {
        var scheme = r.Scheme ?? throw new InvalidOperationException("Поверхность ещё не рассчитана.");
        var mesh = scheme.Surface.DoubleSided();
        if (includeCurves)
            foreach (var c in scheme.Curves.Where(c => c.Points.Count > 1))
                mesh.Append(MeshBuilder.Tube(c.Points, tubeRadius));
        return ToFrame(mesh, r, frame);
    }

    public static Mesh ToFrame(Mesh artMesh, CaseResult r, ExportFrame frame)
    {
        if (frame == ExportFrame.Articulator) return artMesh;
        var t = r.ScanToArticulator ?? throw new InvalidOperationException("Нет переноса в систему артикулятора.");
        return artMesh.Transformed(t.Inverse());
    }

    public static void WriteSurfaceStl(string path, CaseResult r, ExportFrame frame, bool includeCurves) =>
        StlIO.WriteBinary(path, SurfaceMesh(r, frame, includeCurves), "Kuwata occlusal surface");

    /// <summary>
    /// Параметры для CAD/артикулятора: настройки Artex CR, матрица «скан → артикулятор»,
    /// ключевые точки и наклоны скатов.
    /// </summary>
    public static void WriteParametersJson(string path, KuwataProject p, CaseResult r)
    {
        var data = new
        {
            caseName = p.CaseName,
            articulator = "Amann Girrbach Artex CR",
            coordinateSystem = "X вправо (пациента), Y вперёд, Z вверх; начало — середина шарнирной оси; XY — ось-орбитальная плоскость; мм",
            scanToArticulator4x4 = r.ScanToArticulator?.ToRowMajor4x4(),
            artexSettings = r.Artex,
            guidance = r.Angles?.Guidance,
            slopes = r.Angles is { } a ? new { right = a.Right, left = a.Left } : null,
            points = r.Scheme?.Points.Select(x => new { name = x.Name, xyz = x.Point.ToArray() }),
            longCentricMm = p.LongCentricMm,
        };
        File.WriteAllText(path, JsonSerializer.Serialize(data, ProjectFile.Json));
    }
}
