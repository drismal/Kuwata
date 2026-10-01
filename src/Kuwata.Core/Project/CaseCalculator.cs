using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;
using Kuwata.Core.Method;

namespace Kuwata.Core.Project;

/// <summary>Источник значения — для отчёта.</summary>
public enum ValueSource { Manual, Landmarks, AutoFromScans, Missing }

public sealed record ResolvedValue(double? Value, ValueSource Source);

/// <summary>Настройки для выставления на Artex CR.</summary>
public sealed record ArtexSettings(
    double SagittalPathRight, double SagittalPathLeft,
    double BennettRight, double BennettLeft,
    double IncisalTableProtrusive, double IncisalTableLateralRight, double IncisalTableLateralLeft,
    double RetrusionLongCentricMm,
    bool RangesVerified);

/// <summary>Наклон траектории точки в кинематике против значения по формуле Куваты, °.</summary>
public sealed record PathComparison(string Point, string Movement, string Formula, double FormulaValue, double KinematicValue);

public sealed class CaseResult
{
    public RigidTransform? ScanToArticulator { get; set; }
    public Vec3? CondyleRightWorld { get; set; }
    public Vec3? CondyleLeftWorld { get; set; }
    public ResolvedValue Overbite { get; set; } = new(null, ValueSource.Missing);
    public ResolvedValue Overjet { get; set; } = new(null, ValueSource.Missing);
    public ResolvedValue A1 { get; set; } = new(null, ValueSource.Missing);
    public ResolvedValue A2 { get; set; } = new(null, ValueSource.Missing);
    public ResolvedValue A3 { get; set; } = new(null, ValueSource.Missing);
    public KuwataResult? Angles { get; set; }
    public OcclusalScheme? Scheme { get; set; }
    public IReadOnlyList<ToothSize> Teeth { get; set; } = [];
    public ArtexSettings? Artex { get; set; }
    /// <summary>Режущий край нижнего центрального резца в системе артикулятора (резцовая точка кинематики).</summary>
    public Vec3? LowerIncisorTipArt { get; set; }
    public ArticulatorKinematics? Kinematics { get; set; }
    /// <summary>Динамические наклоны траекторий бугров первых моляров (сравнение с формулами Куваты).</summary>
    public IReadOnlyList<PathComparison> PathComparisons { get; set; } = [];
    public List<string> Errors { get; } = new();
    public List<string> Warnings { get; } = new();
}

/// <summary>
/// Полный расчёт случая. Каждый этап выполняется независимо: ошибка одного этапа
/// попадает в Errors и не мешает остальным.
/// Порядок (по Кувате): фронтальная направляющая → углы скатов → окклюзионная плоскость.
/// </summary>
public static class CaseCalculator
{
    public static CaseResult Compute(KuwataProject p, ArtexCrConfig? artex = null)
    {
        artex ??= ArtexCrConfig.Default;
        var r = new CaseResult();

        Try(r, "Перенос в систему артикулятора", () => r.ScanToArticulator = Registration(p, r, artex));
        var T = r.ScanToArticulator;

        Try(r, "Пропорции зубов", () => r.Teeth = ToothProportions.Calculate(p.IncisorWidth, p.IncisorLength, p.Proportions, p.WardRatio));

        Try(r, "Перекрытие резцов", () => ResolveIncisors(p, r, T));
        Try(r, "Направляющая клыков", () => ResolveCanines(p, r, T));

        if (r.A1.Value is { } a1 && r.A2.Value is { } a2 && r.A3.Value is { } a3)
        {
            Try(r, "Углы Куваты", () =>
            {
                r.Angles = KuwataAngles.Calculate(p.Joint, new GuidanceAngles(a1, a2, a3));
                r.Warnings.AddRange(r.Angles.Warnings);
            });
        }
        else
        {
            r.Errors.Add("Углы Куваты: не хватает A1/A2/A3 — укажите клыки и резцы или введите углы вручную.");
        }

        if (T is not null)
            Try(r, "Кривая Шпее / Уилсона", () => r.Scheme = BuildScheme(p, T, artex));

        if (r.Angles is { } ang && T is not null)
            Try(r, "Кинематика", () => BuildKinematics(p, r, ang, T, artex));

        if (r.Angles is { } k)
        {
            r.Artex = new ArtexSettings(
                p.Joint.SagittalPathRight, p.Joint.SagittalPathLeft,
                k.BennettRight, k.BennettLeft,
                k.Guidance.A3, k.Guidance.A1, k.Guidance.A2,
                p.LongCentricMm, artex.Verified);
            CheckRange(r, "ССП справа", p.Joint.SagittalPathRight, artex.SagittalCondylePathDeg, artex.Verified);
            CheckRange(r, "ССП слева", p.Joint.SagittalPathLeft, artex.SagittalCondylePathDeg, artex.Verified);
            CheckRange(r, "Беннет справа", k.BennettRight, artex.BennettAngleDeg, artex.Verified);
            CheckRange(r, "Беннет слева", k.BennettLeft, artex.BennettAngleDeg, artex.Verified);
            CheckRange(r, "Long centric", p.LongCentricMm, artex.RetrusionMm, artex.Verified);
        }
        return r;
    }

    private static void CheckRange(CaseResult r, string name, double v, ArtexCrConfig.Range range, bool verified)
    {
        if (!range.Contains(v))
            r.Warnings.Add($"{name} = {v:0.0} вне диапазона Artex CR {range}{(verified ? "" : " (диапазон не сверен с документацией)")}.");
    }

    private static void Try(CaseResult r, string stage, Action a)
    {
        try { a(); }
        catch (Exception e) when (e is ArgumentException or InvalidOperationException)
        {
            r.Errors.Add($"{stage}: {e.Message}");
        }
    }

    private static Vec3 Need(KuwataProject p, Landmark l) =>
        p.GetLandmark(l) ?? throw new InvalidOperationException($"не указан ориентир «{KuwataProject.LandmarkTitle(l)}».");

    private static RigidTransform Registration(KuwataProject p, CaseResult r, ArtexCrConfig artex)
    {
        switch (p.Registration)
        {
            case RegistrationMode.AlreadyInArticulator:
                return RigidTransform.Identity;
            case RegistrationMode.FacebowMatrix:
                return RigidTransform.FromRowMajor4x4(p.FacebowMatrix
                    ?? throw new InvalidOperationException("не задана матрица лицевой дуги."));
            case RegistrationMode.AxisOrbital:
            {
                Vec3 cr = CondyleCenter(p, MeshRole.CondyleRight, Landmark.CondyleRight);
                Vec3 cl = CondyleCenter(p, MeshRole.CondyleLeft, Landmark.CondyleLeft);
                r.CondyleRightWorld = cr;
                r.CondyleLeftWorld = cl;
                double icd = Vec3.Distance(cr, cl);
                if (Math.Abs(icd - artex.IntercondylarDistanceMm) > 15)
                    r.Warnings.Add($"Межмыщелковое расстояние пациента {icd:0.0} мм заметно отличается от {artex.IntercondylarDistanceMm} мм артикулятора.");
                return ArticulatorFrame.FromAxisOrbital(cr, cl, Need(p, Landmark.Orbitale));
            }
            case RegistrationMode.AverageValues:
                return ArticulatorFrame.FromAverageValues(
                    Need(p, Landmark.LowerIncisalPoint),
                    Need(p, Landmark.LowerFirstMolarRight),
                    Need(p, Landmark.LowerFirstMolarLeft),
                    p.AverageValues, artex);
            default:
                throw new InvalidOperationException("неизвестный режим переноса.");
        }
    }

    private static Vec3 CondyleCenter(KuwataProject p, MeshRole role, Landmark lm)
    {
        if (p.CondyleCentersFromMeshes && p.Meshes.TryGetValue(role, out var mesh))
            return SphereFit.CondyleHead(mesh).Center;
        return Need(p, lm);
    }

    private static void ResolveIncisors(KuwataProject p, CaseResult r, RigidTransform? T)
    {
        OverlapValues? measured = null;
        ValueSource src = ValueSource.Missing;
        if (T is not null)
        {
            var u = p.GetLandmark(Landmark.UpperIncisorTip);
            var l = p.GetLandmark(Landmark.LowerIncisorTip);
            if (u is { } uu && l is { } ll)
            {
                measured = Overlap.FromIncisorTips(T.Apply(uu), T.Apply(ll));
                r.LowerIncisorTipArt = T.Apply(ll);
                src = ValueSource.Landmarks;
            }
            else if (p.Meshes.TryGetValue(MeshRole.Upper, out var up) && p.Meshes.TryGetValue(MeshRole.Lower, out var lo))
            {
                var (ut, lt) = Overlap.DetectIncisorTips(up.Transformed(T), lo.Transformed(T));
                measured = Overlap.FromIncisorTips(ut, lt);
                r.LowerIncisorTipArt = lt;
                src = ValueSource.AutoFromScans;
            }
        }

        r.Overbite = p.Manual.Overbite is { } ob ? new(ob, ValueSource.Manual) : new(measured?.Overbite, src);
        r.Overjet = p.Manual.Overjet is { } oj ? new(oj, ValueSource.Manual) : new(measured?.Overjet, src);

        if (p.Manual.A3 is { } a3) r.A3 = new(a3, ValueSource.Manual);
        else if (r.Overbite.Value is { } b && r.Overjet.Value is { } j)
        {
            var src3 = r.Overbite.Source == ValueSource.Manual || r.Overjet.Source == ValueSource.Manual ? ValueSource.Manual : src;
            r.A3 = new(Overlap.GuidanceAngle(new OverlapValues(b, j)), src3);
        }
    }

    private static void ResolveCanines(KuwataProject p, CaseResult r, RigidTransform? T)
    {
        r.A1 = Canine(p, T, p.Manual.A1, Landmark.UpperCanineTipRight, Landmark.LowerCanineTipRight);
        r.A2 = Canine(p, T, p.Manual.A2, Landmark.UpperCanineTipLeft, Landmark.LowerCanineTipLeft);
    }

    private static ResolvedValue Canine(KuwataProject p, RigidTransform? T, double? manual, Landmark upper, Landmark lower)
    {
        if (manual is { } m) return new(m, ValueSource.Manual);
        if (T is null) return new(null, ValueSource.Missing);
        var u = p.GetLandmark(upper);
        var l = p.GetLandmark(lower);
        if (u is null || l is null) return new(null, ValueSource.Missing);
        return new(Overlap.GuidanceAngle(Overlap.FromCanineTips(T.Apply(u.Value), T.Apply(l.Value))), ValueSource.Landmarks);
    }

    private static void BuildKinematics(KuwataProject p, CaseResult r, KuwataResult k, RigidTransform T, ArtexCrConfig artex)
    {
        var inc = r.LowerIncisorTipArt
            ?? (p.GetLandmark(Landmark.LowerIncisalPoint) is { } lip ? T.Apply(lip) : (Vec3?)null)
            ?? throw new InvalidOperationException("нет резцовой точки — укажите режущий край нижнего резца или загрузите сканы.");
        var kin = new ArticulatorKinematics(new KinematicsParameters
        {
            SagittalPathRight = p.Joint.SagittalPathRight,
            SagittalPathLeft = p.Joint.SagittalPathLeft,
            BennettRight = k.BennettRight,
            BennettLeft = k.BennettLeft,
            Guidance = k.Guidance,
            IncisalPoint = inc,
            LongCentricMm = p.LongCentricMm,
        }, artex);
        r.Kinematics = kin;

        var list = new List<PathComparison>();
        foreach (var (lm, side, s) in new[] { (Landmark.LowerFirstMolarRight, "6 справа", k.Right), (Landmark.LowerFirstMolarLeft, "6 слева", k.Left) })
        {
            if (p.GetLandmark(lm) is not { } w) continue;
            var pt = T.Apply(w);
            bool right = lm == Landmark.LowerFirstMolarRight;
            list.Add(new(side, "протрузия", right ? "P3 справа" : "P3 слева", s.ProtrusiveSlope, kin.PathAngle(Movement.Protrusion, pt)));
            list.Add(new(side, "рабочая сторона", right ? "WP1" : "WP2", s.WorkingSlope,
                kin.PathAngle(right ? Movement.LaterotrusionRight : Movement.LaterotrusionLeft, pt)));
            list.Add(new(side, "балансирующая сторона", right ? "BP1" : "BP2", s.BalancingSlope,
                kin.PathAngle(right ? Movement.LaterotrusionLeft : Movement.LaterotrusionRight, pt)));
        }
        r.PathComparisons = list;
    }

    /// <summary>
    /// Проверка контактов на текущих сканах (или на загруженной восковой модели / дизайне
    /// вместо нижней челюсти). Тяжёлая операция — запускается по запросу.
    /// </summary>
    public static ContactReport RunContactCheck(KuwataProject p, CaseResult r, ContactSettings? settings = null)
    {
        var T = r.ScanToArticulator ?? throw new InvalidOperationException("Нет переноса в систему артикулятора.");
        var kin = r.Kinematics ?? throw new InvalidOperationException("Кинематика не рассчитана — см. отчёт.");
        if (!p.Meshes.TryGetValue(MeshRole.Upper, out var up) || !p.Meshes.TryGetValue(MeshRole.Lower, out var lo))
            throw new InvalidOperationException("Нужны обе челюсти.");
        double lower6 = r.Teeth.FirstOrDefault(t => !t.Upper && t.Tooth == 6)?.MesioDistal ?? 11.0;
        var zones = ArchZones.FromLandmarks(
            T.Apply(Need(p, Landmark.AspRight)), T.Apply(Need(p, Landmark.AspLeft)),
            T.Apply(Need(p, Landmark.LowerFirstMolarRight)), T.Apply(Need(p, Landmark.LowerFirstMolarLeft)),
            lower6);
        return ContactCheck.Run(up.Transformed(T), lo.Transformed(T), kin, zones, p.LongCentricMm, settings);
    }

    private static OcclusalScheme BuildScheme(KuwataProject p, RigidTransform T, ArtexCrConfig artex)
    {
        SideLandmarks Side(Landmark asp, Landmark m6, Landmark mpsp)
        {
            var mp = p.GetLandmark(mpsp);
            return new SideLandmarks(T.Apply(Need(p, asp)), T.Apply(Need(p, m6)), mp is { } v ? T.Apply(v) : null);
        }
        return OcclusalSurfaceBuilder.Build(
            Side(Landmark.AspRight, Landmark.LowerFirstMolarRight, Landmark.MpspRight),
            Side(Landmark.AspLeft, Landmark.LowerFirstMolarLeft, Landmark.MpspLeft),
            p.Broadrick, p.Surface, artex);
    }
}
