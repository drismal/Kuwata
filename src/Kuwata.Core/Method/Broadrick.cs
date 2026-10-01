using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;

namespace Kuwata.Core.Method;

public enum PosteriorSurveyPoint
{
    /// <summary>Дистощёчный бугор нижнего второго моляра.</summary>
    Mpsp,
    /// <summary>Передняя сторона мыщелкового элемента артикулятора.</summary>
    Cpsp,
}

public sealed class BroadrickSettings
{
    /// <summary>[КНИГА] т.1 с.193: 4″ = 101.6 мм (сфера Монсона).</summary>
    public const double FourInchesMm = 101.6;

    public double RadiusMm { get; set; } = FourInchesMm;
    public PosteriorSurveyPoint PosteriorPoint { get; set; } = PosteriorSurveyPoint.Mpsp;
    /// <summary>[РЕШЕНИЕ] Сдвиг OPSC кзади от точки деления вдоль дуги ASP, мм (Кувата — «немного кзади»).</summary>
    public double OpscShiftMm { get; set; } = 2.0;
}

/// <summary>
/// Анализатор Бродрика (система P.M.S.), т.1 с.153, 185–193. Строится для одной стороны
/// в сагиттальной проекции (плоскость YZ артикулятора — «флаг» Бродрика).
/// Точки 2D: (y, z).
/// </summary>
public sealed class BroadrickSide
{
    public Side Side { get; }
    public double Radius { get; }
    public (double Y, double Z) Asp { get; }
    public (double Y, double Z) Posterior { get; }
    public (double Y, double Z) BisectingPoint { get; }
    public (double Y, double Z) Opsc { get; }
    /// <summary>Абсолютный X точки, в плоскости которой строился флаг (для отображения).</summary>
    public double FlagX { get; }

    private BroadrickSide(Side side, double r, (double, double) asp, (double, double) post,
        (double, double) bis, (double, double) opsc, double flagX)
    {
        Side = side; Radius = r; Asp = asp; Posterior = post; BisectingPoint = bis; Opsc = opsc; FlagX = flagX;
    }

    /// <param name="aspArt">ASP на нижнем клыке, в системе артикулятора.</param>
    /// <param name="posteriorArt">MPSP или CPSP, в системе артикулятора.</param>
    public static BroadrickSide Build(Side side, Vec3 aspArt, Vec3 posteriorArt, BroadrickSettings s)
    {
        double r = s.RadiusMm;
        var a = (aspArt.Y, aspArt.Z);
        var p = (posteriorArt.Y, posteriorArt.Z);

        // Пересечение дуги ASP и дуги задней точки (обе радиуса R) — «точка деления».
        double dy = p.Item1 - a.Item1, dz = p.Item2 - a.Item2;
        double d = Math.Sqrt(dy * dy + dz * dz);
        if (d < 1e-6) throw new ArgumentException("ASP и задняя точка совпадают.");
        if (d > 2 * r) throw new ArgumentException($"Расстояние ASP — задняя точка {d:0.0} мм больше 2R: дуги не пересекаются.");
        double h = Math.Sqrt(r * r - d * d / 4);
        var mid = (a.Item1 + dy / 2, a.Item2 + dz / 2);
        // Нормаль к отрезку; выбираем пересечение выше (центр кривой — над зубами).
        var n = (-dz / d, dy / d);
        var c1 = (mid.Item1 + n.Item1 * h, mid.Item2 + n.Item2 * h);
        var c2 = (mid.Item1 - n.Item1 * h, mid.Item2 - n.Item2 * h);
        var bis = c1.Item2 >= c2.Item2 ? c1 : c2;

        // OPSC — на дуге ASP, сдвиг кзади (к меньшим Y) на длину дуги s.
        double theta0 = Math.Atan2(bis.Item2 - a.Item2, bis.Item1 - a.Item1);
        // Центр над ASP (sinθ > 0): движение кзади (cosθ убывает) = рост θ.
        double theta = theta0 + s.OpscShiftMm / r;
        var opsc = (a.Item1 + r * Math.Cos(theta), a.Item2 + r * Math.Sin(theta));

        return new BroadrickSide(side, r, a, p, bis, opsc, aspArt.X);
    }

    /// <summary>Высота кривой Шпее (нижняя дуга окружности из OPSC радиусом R) в точке y.</summary>
    public double SpeeZ(double y)
    {
        double dy = y - Opsc.Y;
        if (Math.Abs(dy) > Radius) throw new ArgumentOutOfRangeException(nameof(y), "Точка вне дуги кривой Шпее.");
        return Opsc.Z - Math.Sqrt(Radius * Radius - dy * dy);
    }
}

/// <summary>
/// Кривая Уилсона [РЕШЕНИЕ, раздел 5; книга её не задаёт, т.1 с.192].
/// Окружность радиуса R во фронтальной плоскости (XZ) через высоты кривой Шпее справа
/// и слева в области первых моляров. Центр — на серединном перпендикуляре к отрезку
/// между этими точками, над ними (при равных высотах — на средней линии).
/// Щёчные бугры нижних моляров выше язычных (окружность вогнута вверх).
/// </summary>
public sealed class WilsonCurve
{
    public double Radius { get; }
    public double CenterX { get; }
    public double CenterZ { get; }
    public double PlaneY { get; }
    public (double X, double Z) Right { get; }
    public (double X, double Z) Left { get; }

    public WilsonCurve(double radius, (double X, double Z) right, (double X, double Z) left, double planeY)
    {
        Radius = radius; Right = right; Left = left; PlaneY = planeY;
        double dx = right.X - left.X, dz = right.Z - left.Z;
        double d = Math.Sqrt(dx * dx + dz * dz);
        if (d > 2 * radius) throw new ArgumentException("Радиус Уилсона меньше половины расстояния между молярами.");
        double h = Math.Sqrt(radius * radius - d * d / 4);
        double mx = (right.X + left.X) / 2, mz = (right.Z + left.Z) / 2;
        // Нормаль к отрезку, направленная вверх.
        double nx = -dz / d, nz = dx / d;
        if (nz < 0) { nx = -nx; nz = -nz; }
        CenterX = mx + nx * h;
        CenterZ = mz + nz * h;
    }

    public double Z(double x)
    {
        double dx = x - CenterX;
        if (Math.Abs(dx) > Radius) throw new ArgumentOutOfRangeException(nameof(x));
        return CenterZ - Math.Sqrt(Radius * Radius - dx * dx);
    }
}
