namespace Kuwata.Core.Method;

/// <summary>Суставные параметры пациента (вводятся вручную, градусы).</summary>
public sealed class JointParameters
{
    /// <summary>ССП справа, °.</summary>
    public double SagittalPathRight { get; set; } = 45;
    /// <summary>ССП слева, °.</summary>
    public double SagittalPathLeft { get; set; } = 45;
    /// <summary>Угол Беннета справа, °. null — рассчитать по Ханау.</summary>
    public double? BennettRight { get; set; }
    /// <summary>Угол Беннета слева, °. null — рассчитать по Ханау.</summary>
    public double? BennettLeft { get; set; }
}

/// <summary>Углы фронтальной направляющей (от горизонтали артикулятора), °.</summary>
public sealed record GuidanceAngles(double A1, double A2, double A3);

/// <summary>Результаты по одной стороне.</summary>
public sealed record SideAngles(
    string Side,
    double WorkingSlope,     // WP1 / WP2
    double BalancingSlope,   // BP1 / BP2
    double ProtrusiveSlope,  // P3 своей стороны
    double DeploymentAngle,  // 180 − (WP + BP)
    IReadOnlyList<ToothTolerance> Teeth);

/// <summary>Допуски по зубу (раздел 10).</summary>
public sealed record ToothTolerance(int Tooth, double Margin, double P3, double BP, double WP, double Tolerance);

public sealed record KuwataResult(
    GuidanceAngles Guidance,
    double C1, double C2,
    double BennettRight, bool BennettRightFromHanau,
    double BennettLeft, bool BennettLeftFromHanau,
    SideAngles Right, SideAngles Left,
    IReadOnlyList<string> Warnings);

/// <summary>
/// Формулы Куваты (т.1 с.145–147, 199–201; т.2 с.305):
///   WP1 = (A1 + RC1)/2, RC1 = 0      WP2 = (A2 + RC2)/2, RC2 = 0
///   BP2 = (A1 + C2)/2                BP1 = (A2 + C1)/2
///   P3  = (A3 + C3)/2
///   Угол раскрытия: справа 180 − (WP1 + BP1), слева 180 − (WP2 + BP2).
/// C1 = ССП справа, C2 = ССП слева [РЕШЕНИЕ, раздел 8].
/// C3 берётся по своей стороне: P3_R = (A3 + ССП_R)/2, P3_L = (A3 + ССП_L)/2.
/// </summary>
public static class KuwataAngles
{
    /// <summary>Поворот рабочего мыщелка. У Куваты всегда 0° (раздел 2).</summary>
    public const double RotationWorkingCondyle = 0;

    /// <summary>[КНИГА] т.1 с.136: угол Беннета по Ханау L = H/8 + 12.</summary>
    public static double HanauBennett(double sagittalPath) => sagittalPath / 8 + 12;

    public static double WorkingSlope(double a) => (a + RotationWorkingCondyle) / 2;
    public static double BalancingSlope(double guidance, double balancingCondylePath) => (guidance + balancingCondylePath) / 2;
    public static double ProtrusiveSlope(double a3, double c3) => (a3 + c3) / 2;
    public static double DeploymentAngle(double wp, double bp) => 180 - (wp + bp);

    /// <summary>[РЕШЕНИЕ] Запас для зуба i = 0..3 (зубы 4..7): 5° + 2°·i.</summary>
    public static double Margin(int i) => 5 + 2 * i;

    public static KuwataResult Calculate(JointParameters joint, GuidanceAngles g)
    {
        double c1 = joint.SagittalPathRight;
        double c2 = joint.SagittalPathLeft;

        double wp1 = WorkingSlope(g.A1);
        double wp2 = WorkingSlope(g.A2);
        double bp2 = BalancingSlope(g.A1, c2);
        double bp1 = BalancingSlope(g.A2, c1);
        double p3r = ProtrusiveSlope(g.A3, c1);
        double p3l = ProtrusiveSlope(g.A3, c2);

        var warnings = new List<string>();
        var right = BuildSide("Правая", wp1, bp1, p3r, warnings);
        var left = BuildSide("Левая", wp2, bp2, p3l, warnings);

        if (g.A1 < 20 || g.A1 > 40) warnings.Add($"A1 = {g.A1:0.0}° вне 20–40° — далеко от ориентира Шуйлера 30°.");
        if (g.A2 < 20 || g.A2 > 40) warnings.Add($"A2 = {g.A2:0.0}° вне 20–40° — далеко от ориентира Шуйлера 30°.");
        if (g.A3 <= 0) warnings.Add("A3 ≤ 0 — нет перекрытия резцов.");

        double bR = joint.BennettRight ?? HanauBennett(joint.SagittalPathRight);
        double bL = joint.BennettLeft ?? HanauBennett(joint.SagittalPathLeft);

        return new KuwataResult(g, c1, c2,
            bR, joint.BennettRight is null, bL, joint.BennettLeft is null,
            right, left, warnings);
    }

    private static SideAngles BuildSide(string name, double wp, double bp, double p3, List<string> warnings)
    {
        var teeth = new List<ToothTolerance>();
        for (int i = 0; i < 4; i++)
        {
            double m = Margin(i);
            double p = p3 - m, b = bp - m, w = wp - m;
            double tol = Math.Min(p, b);
            teeth.Add(new ToothTolerance(4 + i, m, p, b, w, tol));
            if (tol < 8) warnings.Add($"{name} сторона, зуб {4 + i}: допуск {tol:0.0}° < 8° — бугры почти плоские.");
        }
        return new SideAngles(name, wp, bp, p3, DeploymentAngle(wp, bp), teeth);
    }

    /// <summary>
    /// Шаблон «бугор — ямка» (т.1 с.208), ближайший к углу раскрытия:
    /// A — 100° (наклон 30°), B — 140° (наклон 20°). Справочно.
    /// </summary>
    public static string NearestCuspTemplate(double deploymentAngle) =>
        Math.Abs(deploymentAngle - 100) <= Math.Abs(deploymentAngle - 140)
            ? "A (100°, наклон бугра к ямке 30°)"
            : "B (140°, наклон бугра к ямке 20°)";
}
