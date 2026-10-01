using Kuwata.Core.Geometry;

namespace Kuwata.Core.Method;

public enum ToothRegion
{
    AnteriorRight, AnteriorLeft,       // резцы и клык (кпереди от ASP)
    PosteriorRight, PosteriorLeft,     // премоляры и первый моляр
    SecondMolarRight, SecondMolarLeft, // вторые моляры
}

public sealed class ContactSettings
{
    /// <summary>Зазор, при котором считаем контакт, мм.</summary>
    public double ContactToleranceMm { get; set; } = 0.1;
    /// <summary>Проникновение глубже положения в ЦО на эту величину — интерференция, мм.</summary>
    public double InterferenceMm { get; set; } = 0.05;
    /// <summary>Шаги движения (путь мыщелка / сдвиг кзади), мм.</summary>
    public double[] Steps { get; set; } = [0.5, 1, 2, 3, 4, 5];
    /// <summary>Вершины нижней дальше этого от верхней в ЦО в проверку не берутся, мм.</summary>
    public double CandidateDistanceMm { get; set; } = 2.0;
    /// <summary>Глубина поиска поверхности на шаге движения (ловит проникновение до этой глубины), мм.</summary>
    public double SearchReachMm { get; set; } = 0.5;
    /// <summary>Прореживание вершин нижней: одна вершина на воксель такого размера, мм.</summary>
    public double SampleVoxelMm { get; set; } = 0.2;
}

/// <summary>Границы участков зубного ряда в системе артикулятора.</summary>
public sealed record ArchZones(double AspYRight, double AspYLeft, double SecondMolarYRight, double SecondMolarYLeft)
{
    /// <param name="lowerSixMesioDistal">Мезиодистальная ширина нижнего 6 (раздел 9 × k), мм.</param>
    public static ArchZones FromLandmarks(Vec3 aspR, Vec3 aspL, Vec3 m6R, Vec3 m6L, double lowerSixMesioDistal)
    {
        // Мезиально-щёчный бугор ≈ 2.5 мм от мезиального края → 7 начинается дистальнее
        // на (ширина 6 − 2.5) мм. [РЕШЕНИЕ]
        double back = lowerSixMesioDistal - 2.5;
        return new ArchZones(aspR.Y - 1, aspL.Y - 1, m6R.Y - back, m6L.Y - back);
    }

    public ToothRegion Classify(Vec3 p)
    {
        bool right = p.X >= 0;
        double asp = right ? AspYRight : AspYLeft;
        double m7 = right ? SecondMolarYRight : SecondMolarYLeft;
        if (p.Y >= asp) return right ? ToothRegion.AnteriorRight : ToothRegion.AnteriorLeft;
        if (p.Y < m7) return right ? ToothRegion.SecondMolarRight : ToothRegion.SecondMolarLeft;
        return right ? ToothRegion.PosteriorRight : ToothRegion.PosteriorLeft;
    }
}

public sealed record RegionStat(int Contacts, int Interferences, double MinDistance);

public sealed record StepResult(Movement Movement, double Amount, IReadOnlyDictionary<ToothRegion, RegionStat> Regions,
    IReadOnlyList<Vec3> ContactPoints);

public sealed record Verdict(Movement Movement, bool Ok, string Text);

public sealed class ContactReport
{
    public required IReadOnlyList<StepResult> Steps { get; init; }
    public required IReadOnlyList<Verdict> Verdicts { get; init; }
}

/// <summary>
/// Проверки на артикуляторе (раздел 11, цели окклюзии — раздел 1):
///   протрузия — нет контактов жевательных зубов;
///   латеротрузия — нет контактов на балансирующей стороне и на рабочем втором моляре,
///                  на рабочей стороне контакт есть (групповая функция);
///   ретрузия — в пределах long centric нет интерференций на жевательных зубах.
/// Нижняя челюсть двигается, верхняя неподвижна; обе — в системе артикулятора.
/// </summary>
public static class ContactCheck
{
    public static ContactReport Run(Mesh upperArt, Mesh lowerArt, ArticulatorKinematics kin, ArchZones zones,
        double longCentricMm, ContactSettings? settings = null)
    {
        settings ??= new ContactSettings();
        var grid = new ProximityGrid(upperArt);

        // Кандидаты — вершины нижней вблизи верхней в ЦО (грубо, по занятым ячейкам);
        // исходное расстояние в ЦО — точно, если поверхность рядом, иначе «далеко».
        var seen = new HashSet<(long, long, long)>();
        var near = new List<Vec3>();
        double vox = settings.SampleVoxelMm;
        foreach (var v in lowerArt.Vertices)
        {
            if (vox > 0 && !seen.Add(((long)Math.Floor(v.X / vox), (long)Math.Floor(v.Y / vox), (long)Math.Floor(v.Z / vox)))) continue;
            if (grid.HasTrianglesNear(v, settings.CandidateDistanceMm)) near.Add(v);
        }
        var d0s = new double[near.Count];
        Parallel.For(0, near.Count, i => d0s[i] = grid.SignedDistance(near[i], settings.SearchReachMm) ?? double.PositiveInfinity);
        var cand = near.Select((v, i) => (P: v, D0: d0s[i], R: zones.Classify(v))).ToList();

        var steps = new List<StepResult>();
        foreach (var m in Enum.GetValues<Movement>())
        {
            var amounts = m == Movement.Retrusion
                ? settings.Steps.Where(a => a < longCentricMm).Append(longCentricMm).Where(a => a > 0).Distinct().ToArray()
                : settings.Steps;
            foreach (var a in amounts)
                steps.Add(Step(m, a, kin.Pose(m, a), grid, cand, settings));
        }
        return new ContactReport { Steps = steps, Verdicts = Judge(steps, longCentricMm) };
    }

    private static StepResult Step(Movement m, double amount, RigidTransform pose, ProximityGrid grid,
        List<(Vec3 P, double D0, ToothRegion R)> cand, ContactSettings s)
    {
        var stats = Enum.GetValues<ToothRegion>().ToDictionary(r => r, _ => (c: 0, i: 0, min: double.MaxValue));
        var pts = new List<Vec3>();
        double reach = Math.Max(s.ContactToleranceMm, s.SearchReachMm);
        var moved = new Vec3[cand.Count];
        var dist = new double?[cand.Count];
        Parallel.For(0, cand.Count, i =>
        {
            moved[i] = pose.Apply(cand[i].P);
            dist[i] = grid.SignedDistance(moved[i], reach);
        });
        for (int i = 0; i < cand.Count; i++)
        {
            var (_, d0, region) = cand[i];
            var p = moved[i];
            if (dist[i] is not { } dd) continue;
            var st = stats[region];
            if (dd < st.min) st.min = dd;
            if (dd < s.ContactToleranceMm)
            {
                st.c++;
                if (pts.Count < 2000) pts.Add(p);
            }
            if (dd < -s.InterferenceMm && dd < Math.Min(d0, 0) - s.InterferenceMm) st.i++;
            stats[region] = st;
        }
        return new StepResult(m, amount,
            stats.ToDictionary(kv => kv.Key, kv => new RegionStat(kv.Value.c, kv.Value.i,
                kv.Value.min == double.MaxValue ? double.NaN : kv.Value.min)),
            pts);
    }

    private static List<Verdict> Judge(List<StepResult> steps, double lc)
    {
        var v = new List<Verdict>();
        static int Sum(StepResult s, params ToothRegion[] r) => r.Sum(x => s.Regions[x].Contacts);
        static int Interf(StepResult s, params ToothRegion[] r) => r.Sum(x => s.Regions[x].Interferences);
        string Amounts(IEnumerable<StepResult> s) => string.Join(", ", s.Select(x => x.Amount.ToString("0.#")));

        var posterior = new[] { ToothRegion.PosteriorRight, ToothRegion.PosteriorLeft, ToothRegion.SecondMolarRight, ToothRegion.SecondMolarLeft };
        var pro = steps.Where(s => s.Movement == Movement.Protrusion).ToList();
        var bad = pro.Where(s => Sum(s, posterior) > 0).ToList();
        v.Add(bad.Count == 0
            ? new(Movement.Protrusion, true, "Протрузия: жевательные зубы не контактируют.")
            : new(Movement.Protrusion, false, $"Протрузия: контакты жевательных зубов на шагах {Amounts(bad)} мм."));

        foreach (var (mv, name, workPost, workM7, workAnt, balance) in new[]
        {
            (Movement.LaterotrusionRight, "Латеротрузия вправо", ToothRegion.PosteriorRight, ToothRegion.SecondMolarRight, ToothRegion.AnteriorRight,
                new[] { ToothRegion.PosteriorLeft, ToothRegion.SecondMolarLeft }),
            (Movement.LaterotrusionLeft, "Латеротрузия влево", ToothRegion.PosteriorLeft, ToothRegion.SecondMolarLeft, ToothRegion.AnteriorLeft,
                new[] { ToothRegion.PosteriorRight, ToothRegion.SecondMolarRight }),
        })
        {
            var ls = steps.Where(s => s.Movement == mv).ToList();
            var bal = ls.Where(s => Sum(s, balance) > 0).ToList();
            v.Add(bal.Count == 0
                ? new(mv, true, $"{name}: на балансирующей стороне контактов нет.")
                : new(mv, false, $"{name}: БАЛАНСИРУЮЩИЕ контакты на шагах {Amounts(bal)} мм."));
            var m7 = ls.Where(s => Sum(s, workM7) > 0).ToList();
            v.Add(m7.Count == 0
                ? new(mv, true, $"{name}: рабочий второй моляр не контактирует.")
                : new(mv, false, $"{name}: контакт рабочего второго моляра на шагах {Amounts(m7)} мм."));
            var first = ls.FirstOrDefault();
            bool group = first is not null && Sum(first, workAnt) > 0 && Sum(first, workPost) > 0;
            v.Add(group
                ? new(mv, true, $"{name}: групповая функция — контакт клыка и жевательных зубов рабочей стороны.")
                : new(mv, false, $"{name}: на первом шаге нет одновременного контакта клыка и жевательных зубов рабочей стороны (групповая функция не подтверждена)."));
        }

        var ret = steps.Where(s => s.Movement == Movement.Retrusion).ToList();
        var ri = ret.Where(s => Interf(s, posterior) > 0).ToList();
        v.Add(ri.Count == 0
            ? new(Movement.Retrusion, true, $"Ретрузия в пределах long centric {lc:0.0#} мм: интерференций на жевательных зубах нет.")
            : new(Movement.Retrusion, false, $"Ретрузия: интерференции на жевательных зубах при сдвиге {Amounts(ri)} мм."));
        return v;
    }
}
