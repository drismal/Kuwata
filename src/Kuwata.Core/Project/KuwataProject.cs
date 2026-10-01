using System.Text.Json.Serialization;
using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;
using Kuwata.Core.Method;

namespace Kuwata.Core.Project;

public enum RegistrationMode
{
    /// <summary>Сканы уже в системе артикулятора (экспорт из виртуального артикулятора / лицевой дуги).</summary>
    AlreadyInArticulator,
    /// <summary>Лицевая дуга: матрица 4×4 «скан → артикулятор» задана численно.</summary>
    FacebowMatrix,
    /// <summary>По КТ: центры суставных головок + орбитальная точка.</summary>
    AxisOrbital,
    /// <summary>Без КТ: средние значения (Бонвиль, Балквилл, 10°).</summary>
    AverageValues,
}

public enum MeshRole { Upper, Lower, CondyleRight, CondyleLeft }

/// <summary>Ориентиры. Хранятся в исходных (мировых) координатах сканов/КТ.</summary>
public enum Landmark
{
    CondyleRight, CondyleLeft, Orbitale,
    LowerIncisalPoint,
    UpperIncisorTip, LowerIncisorTip,
    UpperCanineTipRight, LowerCanineTipRight,
    UpperCanineTipLeft, LowerCanineTipLeft,
    AspRight, AspLeft,
    LowerFirstMolarRight, LowerFirstMolarLeft,
    MpspRight, MpspLeft,
}

/// <summary>Ручные значения. null — брать расчётное.</summary>
public sealed class ManualOverrides
{
    public double? Overbite { get; set; }
    public double? Overjet { get; set; }
    public double? A1 { get; set; }
    public double? A2 { get; set; }
    public double? A3 { get; set; }
}

/// <summary>Один клинический случай = один файл проекта.</summary>
public sealed class KuwataProject
{
    public int FormatVersion { get; set; } = 1;
    public string CaseName { get; set; } = "";
    public string Notes { get; set; } = "";

    public RegistrationMode Registration { get; set; } = RegistrationMode.AlreadyInArticulator;
    /// <summary>Матрица 4×4 по строкам для режима FacebowMatrix.</summary>
    public double[]? FacebowMatrix { get; set; }
    /// <summary>В режиме AxisOrbital: центры головок подбираются сферой по STL мыщелков, если они загружены.</summary>
    public bool CondyleCentersFromMeshes { get; set; } = true;
    public AverageValueSettings AverageValues { get; set; } = new();

    public Dictionary<Landmark, double[]> Landmarks { get; set; } = new();

    public JointParameters Joint { get; set; } = new();
    public double IncisorWidth { get; set; } = 8.5;
    public double IncisorLength { get; set; } = 10.5;
    public ManualOverrides Manual { get; set; } = new();

    public BroadrickSettings Broadrick { get; set; } = new();
    public SurfaceSettings Surface { get; set; } = new();

    /// <summary>[РЕШЕНИЕ] Ретрузионный контроль = long centric, свобода кзади, мм.</summary>
    public double LongCentricMm { get; set; } = 0.5;

    public ProportionSystem Proportions { get; set; } = ProportionSystem.Wheeler;
    public double WardRatio { get; set; } = 0.70;

    [JsonIgnore]
    public Dictionary<MeshRole, Mesh> Meshes { get; } = new();

    public Vec3? GetLandmark(Landmark l) => Landmarks.TryGetValue(l, out var a) ? Vec3.FromArray(a) : null;
    public void SetLandmark(Landmark l, Vec3 p) => Landmarks[l] = p.ToArray();
    public void ClearLandmark(Landmark l) => Landmarks.Remove(l);

    public static string LandmarkTitle(Landmark l) => l switch
    {
        Landmark.CondyleRight => "Центр суставной головки справа",
        Landmark.CondyleLeft => "Центр суставной головки слева",
        Landmark.Orbitale => "Орбитальная точка",
        Landmark.LowerIncisalPoint => "Резцовая точка (нижние центральные резцы)",
        Landmark.UpperIncisorTip => "Режущий край верхнего центрального резца",
        Landmark.LowerIncisorTip => "Режущий край нижнего центрального резца",
        Landmark.UpperCanineTipRight => "Бугор верхнего клыка справа",
        Landmark.LowerCanineTipRight => "Бугор нижнего клыка справа",
        Landmark.UpperCanineTipLeft => "Бугор верхнего клыка слева",
        Landmark.LowerCanineTipLeft => "Бугор нижнего клыка слева",
        Landmark.AspRight => "ASP справа (нижний клык, между вершиной и дистальным краем)",
        Landmark.AspLeft => "ASP слева",
        Landmark.LowerFirstMolarRight => "Мезиально-щёчный бугор нижнего 6 справа",
        Landmark.LowerFirstMolarLeft => "Мезиально-щёчный бугор нижнего 6 слева",
        Landmark.MpspRight => "MPSP справа (дистощёчный бугор нижнего 7)",
        Landmark.MpspLeft => "MPSP слева",
        _ => l.ToString(),
    };
}
