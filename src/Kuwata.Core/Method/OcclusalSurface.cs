using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;

namespace Kuwata.Core.Method;

/// <summary>Ориентиры одной стороны в системе артикулятора.</summary>
public sealed record SideLandmarks(
    Vec3 Asp,                 // нижний клык, между вершиной и дистальным краем
    Vec3 LowerFirstMolar,     // мезиально-щёчный бугор нижнего первого моляра
    Vec3? Mpsp);              // дистощёчный бугор нижнего второго моляра (если выбран MPSP)

public sealed class SurfaceSettings
{
    /// <summary>Ширина полосы внутрь (язычно) от линии щёчных бугров, мм.</summary>
    public double LingualWidthMm { get; set; } = 10;
    /// <summary>Ширина полосы наружу (щёчно), мм.</summary>
    public double BuccalWidthMm { get; set; } = 2;
    /// <summary>Длина зоны жевательных зубов кзади от ASP, если MPSP не задан, мм.</summary>
    public double PosteriorLengthMm { get; set; } = 50;
    /// <summary>Радиус кривой Уилсона [РЕШЕНИЕ]: по умолчанию 4″.</summary>
    public double WilsonRadiusMm { get; set; } = BroadrickSettings.FourInchesMm;
    public double StepMm { get; set; } = 0.5;
}

/// <summary>Именованная ломаная для отображения и экспорта.</summary>
public sealed record Polyline(string Name, IReadOnlyList<Vec3> Points);
public sealed record NamedPoint(string Name, Vec3 Point);

public sealed class OcclusalScheme
{
    public required BroadrickSide Right { get; init; }
    public required BroadrickSide Left { get; init; }
    public required WilsonCurve Wilson { get; init; }
    public required Mesh Surface { get; init; }
    public required IReadOnlyList<Polyline> Curves { get; init; }
    public required IReadOnlyList<NamedPoint> Points { get; init; }
}

/// <summary>
/// Окклюзионная поверхность нижних жевательных зубов:
///   кривая Шпее — по Бродрику на каждой стороне (вдоль линии щёчных бугров),
///   кривая Уилсона — поперечная поправка W(x) − W(x_щёчн).
/// z(x, y) = Шпее_стороны(y) + W(x) − W(x_щёчн(y)).
/// Всё в системе артикулятора.
/// </summary>
public static class OcclusalSurfaceBuilder
{
    public static OcclusalScheme Build(SideLandmarks right, SideLandmarks left,
        BroadrickSettings broadrick, SurfaceSettings surface, ArtexCrConfig? artex = null)
    {
        artex ??= ArtexCrConfig.Default;
        var br = BuildSide(Side.Right, right, broadrick, artex);
        var bl = BuildSide(Side.Left, left, broadrick, artex);

        double planeY = (right.LowerFirstMolar.Y + left.LowerFirstMolar.Y) / 2;
        var wilson = new WilsonCurve(surface.WilsonRadiusMm,
            (right.LowerFirstMolar.X, br.SpeeZ(right.LowerFirstMolar.Y)),
            (left.LowerFirstMolar.X, bl.SpeeZ(left.LowerFirstMolar.Y)),
            planeY);

        var mesh = new Mesh();
        var curves = new List<Polyline>();
        foreach (var (lm, b, name) in new[] { (right, br, "справа"), (left, bl, "слева") })
        {
            var (yRear, yFront) = YRange(lm, surface);
            Func<double, double> xb = BuccalLine(lm);
            double sign = b.Side == Side.Right ? 1 : -1;
            int ny = Math.Max(2, (int)Math.Ceiling((yFront - yRear) / surface.StepMm) + 1);
            int nx = Math.Max(2, (int)Math.Ceiling((surface.LingualWidthMm + surface.BuccalWidthMm) / surface.StepMm) + 1);
            int baseIdx = mesh.Vertices.Count;
            for (int j = 0; j < ny; j++)
            {
                double y = yRear + (yFront - yRear) * j / (ny - 1);
                double xBuccal = xb(y);
                double zSpee = b.SpeeZ(y);
                for (int i = 0; i < nx; i++)
                {
                    // От язычного края к щёчному.
                    double off = -surface.LingualWidthMm + (surface.LingualWidthMm + surface.BuccalWidthMm) * i / (nx - 1);
                    double x = xBuccal + sign * off;
                    mesh.AddVertex(new Vec3(x, y, zSpee + wilson.Z(x) - wilson.Z(xBuccal)));
                }
            }
            for (int j = 0; j < ny - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = baseIdx + j * nx + i, c = a + nx;
                    // Нормаль вверх (+Z) для обеих сторон.
                    if (sign > 0) { mesh.AddTriangle(a, a + 1, c + 1); mesh.AddTriangle(a, c + 1, c); }
                    else { mesh.AddTriangle(a, c + 1, a + 1); mesh.AddTriangle(a, c, c + 1); }
                }

            var spee = new List<Vec3>();
            for (int j = 0; j < ny; j++)
            {
                double y = yRear + (yFront - yRear) * j / (ny - 1);
                spee.Add(new Vec3(xb(y), y, b.SpeeZ(y)));
            }
            curves.Add(new Polyline($"Кривая Шпее {name}", spee));

            // Дуга Бродрика во «флаге» (плоскость x = x(ASP)) от ASP до задней точки.
            var arc = new List<Vec3>();
            double yEnd = b.Posterior.Y;
            int na = Math.Max(2, (int)Math.Ceiling(Math.Abs(b.Asp.Y - yEnd) / 1.0) + 1);
            for (int k = 0; k < na; k++)
            {
                double y = b.Asp.Y + (yEnd - b.Asp.Y) * k / (na - 1);
                if (Math.Abs(y - b.Opsc.Y) <= b.Radius) arc.Add(new Vec3(b.FlagX, y, b.SpeeZ(y)));
            }
            curves.Add(new Polyline($"Дуга Бродрика {name}", arc));
        }

        var wl = new List<Vec3>();
        double x0 = left.LowerFirstMolar.X, x1 = right.LowerFirstMolar.X;
        for (int k = 0; k <= 60; k++)
        {
            double x = x0 + (x1 - x0) * k / 60.0;
            wl.Add(new Vec3(x, planeY, wilson.Z(x)));
        }
        curves.Add(new Polyline("Кривая Уилсона", wl));

        var pts = new List<NamedPoint>();
        foreach (var b in new[] { br, bl })
        {
            string s = b.Side == Side.Right ? "R" : "L";
            pts.Add(new NamedPoint($"ASP {s}", new Vec3(b.FlagX, b.Asp.Y, b.Asp.Z)));
            pts.Add(new NamedPoint($"{(broadrick.PosteriorPoint == PosteriorSurveyPoint.Mpsp ? "MPSP" : "CPSP")} {s}",
                new Vec3(b.FlagX, b.Posterior.Y, b.Posterior.Z)));
            pts.Add(new NamedPoint($"Точка деления {s}", new Vec3(b.FlagX, b.BisectingPoint.Y, b.BisectingPoint.Z)));
            pts.Add(new NamedPoint($"OPSC {s}", new Vec3(b.FlagX, b.Opsc.Y, b.Opsc.Z)));
        }
        pts.Add(new NamedPoint("Центр Уилсона", new Vec3(wilson.CenterX, planeY, wilson.CenterZ)));

        return new OcclusalScheme { Right = br, Left = bl, Wilson = wilson, Surface = mesh, Curves = curves, Points = pts };
    }

    private static BroadrickSide BuildSide(Side side, SideLandmarks lm, BroadrickSettings s, ArtexCrConfig artex)
    {
        Vec3 posterior = s.PosteriorPoint switch
        {
            PosteriorSurveyPoint.Mpsp => lm.Mpsp ?? throw new InvalidOperationException(
                $"Не указана точка MPSP ({(side == Side.Right ? "справа" : "слева")}). Укажите её или выберите CPSP."),
            _ => ArticulatorFrame.Cpsp(side, artex),
        };
        return BroadrickSide.Build(side, lm.Asp, posterior, s);
    }

    private static (double Rear, double Front) YRange(SideLandmarks lm, SurfaceSettings s)
    {
        double front = lm.Asp.Y;
        double rear = lm.Mpsp is { } m ? m.Y - 2 : front - s.PosteriorLengthMm;
        if (rear >= front) throw new InvalidOperationException("Задняя граница зоны жевательных зубов впереди ASP — проверьте ориентиры.");
        return (rear, front);
    }

    /// <summary>Линия щёчных бугров в плане: прямая через ASP и первый моляр.</summary>
    private static Func<double, double> BuccalLine(SideLandmarks lm)
    {
        var a = lm.Asp; var m = lm.LowerFirstMolar;
        double dy = a.Y - m.Y;
        if (Math.Abs(dy) < 1e-6) throw new InvalidOperationException("ASP и первый моляр на одной поперечной линии.");
        return y => m.X + (a.X - m.X) * (y - m.Y) / dy;
    }
}
