namespace Kuwata.Core.Method;

public enum ProportionSystem
{
    /// <summary>[РЕШЕНИЕ] Средние размеры по Уилеру с масштабом от резца (по умолчанию; в книге Куваты пропорций нет).</summary>
    Wheeler,
    /// <summary>Рикеттс: золотая пропорция видимых ширин фронтальных зубов (1 : 0.618).</summary>
    RickettsGolden,
    /// <summary>Уорд: RED — постоянное отношение видимых ширин (по умолчанию 70 %).</summary>
    WardRed,
}

/// <summary>Размеры коронки, мм.</summary>
public sealed record ToothSize(
    int Tooth, bool Upper,
    double MesioDistal, double CrownHeight, double? BuccoLingual,
    double? CuspDistanceMin, double? CuspDistanceMax,
    double? ApparentWidth);

/// <summary>
/// Размеры зубов от центрального резца (docs/KUWATA_METHOD.md, разделы 6, 9).
/// k_ш = ширина резца / 8.5, k_в = длина резца / 10.5.
/// Расстояние между щёчным и язычным буграми — 55–60 % щёчно-язычной ширины [КНИГА т.1 с.65, 159, 194, 216].
/// </summary>
public static class ToothProportions
{
    // Уилер: мезиодистальная ширина × высота коронки, мм (раздел 9).
    private static readonly (double W, double H)[] UpperWheeler =
        [(8.5, 10.5), (6.5, 9.0), (7.5, 10.0), (7.0, 8.5), (6.5, 8.5), (10.0, 7.5), (9.0, 7.0)];
    private static readonly (double W, double H)[] LowerWheeler =
        [(5.0, 9.0), (5.5, 9.5), (7.0, 11.0), (7.0, 8.5), (7.0, 8.0), (11.0, 7.5), (10.5, 7.0)];

    // Щёчно-язычная ширина жевательных зубов 4–7 по Уилеру [ДОПОЛНЕНИЕ, требует сверки].
    private static readonly double[] UpperBuccoLingual = [9.0, 9.0, 11.0, 11.0];
    private static readonly double[] LowerBuccoLingual = [7.5, 8.0, 10.5, 10.0];

    public const double ReferenceIncisorWidth = 8.5;
    public const double ReferenceIncisorLength = 10.5;

    public static IReadOnlyList<ToothSize> Calculate(double incisorWidth, double incisorLength,
        ProportionSystem system = ProportionSystem.Wheeler, double wardRatio = 0.70)
    {
        if (incisorWidth <= 0 || incisorLength <= 0) throw new ArgumentException("Размеры резца должны быть положительными.");
        double kw = incisorWidth / ReferenceIncisorWidth;
        double kh = incisorLength / ReferenceIncisorLength;

        double[]? apparent = system switch
        {
            ProportionSystem.RickettsGolden => [incisorWidth, incisorWidth * 0.618, incisorWidth * 0.618 * 0.618],
            ProportionSystem.WardRed => [incisorWidth, incisorWidth * wardRatio, incisorWidth * wardRatio * wardRatio],
            _ => null,
        };

        var list = new List<ToothSize>();
        foreach (bool upper in new[] { true, false })
        {
            var table = upper ? UpperWheeler : LowerWheeler;
            var bl = upper ? UpperBuccoLingual : LowerBuccoLingual;
            for (int i = 0; i < 7; i++)
            {
                int tooth = i + 1;
                double? b = tooth >= 4 ? bl[tooth - 4] * kw : null;
                list.Add(new ToothSize(tooth, upper,
                    table[i].W * kw, table[i].H * kh, b,
                    b * 0.55, b * 0.60,
                    upper && apparent is not null && tooth <= 3 ? apparent[tooth - 1] : null));
            }
        }
        return list;
    }
}
