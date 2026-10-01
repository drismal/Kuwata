using Kuwata.Core.Geometry;

namespace Kuwata.Core.Articulator;

/// <summary>
/// Система координат артикулятора (docs/KUWATA_METHOD.md, раздел 11):
/// начало — середина шарнирной оси; X — вправо (сторона пациента), Y — вперёд, Z — вверх.
/// Горизонталь XY — ось-орбитальная плоскость. Все углы Куваты отсчитываются от неё.
/// </summary>
public static class ArticulatorFrame
{
    /// <summary>
    /// Сценарий «по КТ» и «по лицевой дуге с ориентирами»: центры суставных головок
    /// справа и слева + орбитальная точка задают ось-орбитальную плоскость.
    /// Возвращает преобразование «мировые координаты → артикулятор».
    /// </summary>
    public static RigidTransform FromAxisOrbital(Vec3 condyleRight, Vec3 condyleLeft, Vec3 orbitale)
    {
        var origin = (condyleRight + condyleLeft) / 2;
        var ex = (condyleRight - condyleLeft).Normalized();
        var toOr = orbitale - origin;
        var ey = toOr - ex * toOr.Dot(ex);
        if (ey.Length < 1e-6) throw new ArgumentException("Орбитальная точка лежит на шарнирной оси.");
        ey = ey.Normalized();
        var ez = ex.Cross(ey);
        return RigidTransform.WorldToFrame(origin, ex, ey, ez);
    }

    /// <summary>
    /// Режим без КТ (средние значения): треугольник Бонвиля, угол Балквилла,
    /// наклон окклюзионной плоскости к ось-орбитальной.
    /// Ориентиры на нижнем скане: резцовая точка (между режущими краями нижних центральных
    /// резцов) и мезиально-щёчные бугры нижних первых моляров справа и слева.
    /// </summary>
    public static RigidTransform FromAverageValues(
        Vec3 lowerIncisalPoint, Vec3 lowerMolarRight, Vec3 lowerMolarLeft,
        AverageValueSettings? settings = null, ArtexCrConfig? artex = null)
    {
        settings ??= new AverageValueSettings();
        artex ??= ArtexCrConfig.Default;

        // Локальный репер окклюзионной плоскости на скане.
        var molarMid = (lowerMolarRight + lowerMolarLeft) / 2;
        var f = (lowerIncisalPoint - molarMid).Normalized();
        var lat = lowerMolarRight - lowerMolarLeft;
        var r = (lat - f * lat.Dot(f)).Normalized();
        var up = r.Cross(f);
        var scanToLocal = RigidTransform.WorldToFrame(lowerIncisalPoint, r, f, up);

        // Тот же репер в системе артикулятора.
        double alpha = Deg(settings.OcclusalPlaneToAxisOrbitalDeg);
        double tau = Deg(settings.OcclusalPlaneToAxisOrbitalDeg + settings.BalkwillAngleDeg);
        double half = artex.IntercondylarDistanceMm / 2;
        if (settings.BonwillSideMm <= half) throw new ArgumentException("Сторона Бонвиля меньше половины межмыщелкового расстояния.");
        double h = Math.Sqrt(settings.BonwillSideMm * settings.BonwillSideMm - half * half);
        var incisalArt = new Vec3(0, h * Math.Cos(tau), -h * Math.Sin(tau));
        var rArt = Vec3.UnitX;
        var fArt = new Vec3(0, Math.Cos(alpha), -Math.Sin(alpha)); // плоскость опускается кпереди
        var upArt = rArt.Cross(fArt);
        var localToArt = new RigidTransform(new double[,]
        {
            { rArt.X, fArt.X, upArt.X },
            { rArt.Y, fArt.Y, upArt.Y },
            { rArt.Z, fArt.Z, upArt.Z },
        }, incisalArt);
        return localToArt.After(scanToLocal);
    }

    /// <summary>Центр суставной головки артикулятора в его системе координат.</summary>
    public static Vec3 CondyleCenter(Side side, ArtexCrConfig? artex = null)
    {
        artex ??= ArtexCrConfig.Default;
        return new Vec3(side == Side.Right ? artex.IntercondylarDistanceMm / 2 : -artex.IntercondylarDistanceMm / 2, 0, 0);
    }

    /// <summary>CPSP — передняя сторона мыщелкового элемента (т.1 с.188–189).</summary>
    public static Vec3 Cpsp(Side side, ArtexCrConfig? artex = null)
    {
        artex ??= ArtexCrConfig.Default;
        return CondyleCenter(side, artex) + new Vec3(0, artex.CondyleElementRadiusMm, 0);
    }

    private static double Deg(double d) => d * Math.PI / 180;
}

public sealed class AverageValueSettings
{
    /// <summary>[РЕШЕНИЕ] Сторона треугольника Бонвиля, мм.</summary>
    public double BonwillSideMm { get; set; } = 110;
    /// <summary>[РЕШЕНИЕ] Угол Балквилла, °.</summary>
    public double BalkwillAngleDeg { get; set; } = 22;
    /// <summary>[РЕШЕНИЕ] Наклон окклюзионной плоскости к ось-орбитальной, °.</summary>
    public double OcclusalPlaneToAxisOrbitalDeg { get; set; } = 10;
}

public enum Side { Right, Left }
