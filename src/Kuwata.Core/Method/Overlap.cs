using Kuwata.Core.Geometry;

namespace Kuwata.Core.Method;

/// <summary>Вертикальное и горизонтальное перекрытие, мм (в осях артикулятора).</summary>
public sealed record OverlapValues(double Overbite, double Overjet);

/// <summary>
/// Перекрытие и углы направляющей.
/// [РЕШЕНИЕ, раздел 8] A3 = arctg(OB/OJ), A1/A2 = arctg(вертикальное/горизонтальное перекрытие клыка).
/// OB и OJ берутся в осях артикулятора (Z — вертикаль, Y — сагиттальная горизонталь),
/// поэтому угол получается от горизонтали артикулятора, как требует книга.
/// Горизонтальная составляющая — расстояние «режущий край — режущий край» (хорда пути скольжения).
/// </summary>
public static class Overlap
{
    /// <summary>По точкам режущих краёв верхнего и нижнего центральных резцов.</summary>
    public static OverlapValues FromIncisorTips(Vec3 upperTip, Vec3 lowerTip) =>
        new(lowerTip.Z - upperTip.Z, upperTip.Y - lowerTip.Y);

    /// <summary>
    /// Для клыков: вертикальное перекрытие по Z, горизонтальное — по X
    /// (направление латеротрузии в первом приближении).
    /// </summary>
    public static OverlapValues FromCanineTips(Vec3 upperTip, Vec3 lowerTip) =>
        new(lowerTip.Z - upperTip.Z, Math.Abs(upperTip.X - lowerTip.X));

    /// <summary>Угол направляющей arctg(OB/OJ), °. OJ ≤ 0 при OB &gt; 0 даёт 90°.</summary>
    public static double GuidanceAngle(OverlapValues o)
    {
        if (o.Overbite <= 0) return 0;
        if (o.Overjet <= 0) return 90;
        return Math.Atan2(o.Overbite, o.Overjet) * 180 / Math.PI;
    }

    /// <summary>
    /// Автоматический поиск режущих краёв центральных резцов на сведённой паре сканов,
    /// уже перенесённых в систему артикулятора: верхний — самая низкая точка
    /// фронтального участка верхней челюсти у средней линии, нижний — самая высокая
    /// точка фронтального участка нижней.
    /// </summary>
    public static (Vec3 UpperTip, Vec3 LowerTip) DetectIncisorTips(Mesh upperArt, Mesh lowerArt,
        double midlineHalfWidth = 4, double anteriorDepth = 10)
    {
        var u = Front(upperArt, midlineHalfWidth, anteriorDepth).MinBy(v => v.Z);
        var l = Front(lowerArt, midlineHalfWidth, anteriorDepth).MaxBy(v => v.Z);
        return (u, l);
    }

    private static List<Vec3> Front(Mesh m, double halfWidth, double depth)
    {
        var mid = m.Vertices.Where(v => Math.Abs(v.X) <= halfWidth).ToList();
        if (mid.Count == 0) throw new InvalidOperationException("У средней линии нет вершин — проверьте перенос в систему артикулятора.");
        double maxY = mid.Max(v => v.Y);
        return mid.Where(v => v.Y >= maxY - depth).ToList();
    }
}
