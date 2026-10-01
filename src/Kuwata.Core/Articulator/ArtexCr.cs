namespace Kuwata.Core.Articulator;

/// <summary>
/// Геометрия и диапазоны регулировок Amann Girrbach Artex CR.
/// ВНИМАНИЕ: значения с Verified = false взяты из открытых описаний и
/// должны быть сверены с инструкцией производителя. В расчёт формул Куваты
/// они не входят — используются для привязки системы координат и предупреждений.
/// </summary>
public sealed class ArtexCrConfig
{
    /// <summary>Межмыщелковое расстояние, мм (Artex — фиксированное, по Бонвилю).</summary>
    public double IntercondylarDistanceMm { get; init; } = 110.0;

    /// <summary>
    /// Радиус мыщелкового элемента, мм. Нужен для точки CPSP
    /// («передняя сторона мыщелкового стержня», т.1 с.188–189).
    /// </summary>
    public double CondyleElementRadiusMm { get; init; } = 5.0;

    public Range SagittalCondylePathDeg { get; init; } = new(-20, 60);
    public Range BennettAngleDeg { get; init; } = new(0, 30);
    public Range RetrusionMm { get; init; } = new(0, 2);

    /// <summary>Все значения выше подтверждены по документации производителя.</summary>
    public bool Verified { get; init; } = false;

    public static ArtexCrConfig Default { get; } = new();

    public readonly record struct Range(double Min, double Max)
    {
        public bool Contains(double v) => v >= Min && v <= Max;
        public override string ToString() => $"{Min}…{Max}";
    }
}
