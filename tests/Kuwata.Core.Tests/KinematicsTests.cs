using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;
using Kuwata.Core.Method;

namespace Kuwata.Core.Tests;

public class KinematicsTests
{
    private static readonly Vec3 Incisal = new(0, 95, -45);
    private static readonly Vec3 CR = new(55, 0, 0), CL = new(-55, 0, 0);

    internal static ArticulatorKinematics Kin(double sspR = 45, double sspL = 45, double benR = 15, double benL = 15,
        double a1 = 40, double a2 = 40, double a3 = 50, double lc = 0.5, Vec3? inc = null) =>
        new(new KinematicsParameters
        {
            SagittalPathRight = sspR, SagittalPathLeft = sspL, BennettRight = benR, BennettLeft = benL,
            Guidance = new GuidanceAngles(a1, a2, a3), IncisalPoint = inc ?? Incisal, LongCentricMm = lc,
        });

    private static double Deg(double r) => r * 180 / Math.PI;

    [Fact]
    public void Protrusion_CondylesFollowSsp_IncisorFollowsA3()
    {
        var t = Kin().Protrusion(3);
        var cr = t.Apply(CR) - CR;
        Assert.Equal(3, cr.Length, 6);
        Assert.Equal(45, Deg(Math.Atan2(-cr.Z, cr.Y)), 6);
        Assert.Equal(0, cr.X, 6);
        var d = t.Apply(Incisal) - Incisal;
        Assert.Equal(50, Deg(Math.Atan2(-d.Z, d.Y)), 6);
        t.CheckOrthonormal();
    }

    [Fact]
    public void Protrusion_Asymmetric_StaysRigid()
    {
        var t = Kin(sspR: 30, sspL: 55).Protrusion(3);
        t.CheckOrthonormal();
        // Горизонтальные составляющие путей мыщелков — вперёд.
        Assert.True((t.Apply(CR) - CR).Y > 0 && (t.Apply(CL) - CL).Y > 0);
    }

    [Fact]
    public void LaterotrusionRight_WorkingCondyleOnlyLateral_BalancingOnPath()
    {
        var k = Kin(sspL: 40, benL: 15, a1: 35);
        var t = k.Laterotrusion(Side.Right, 4);
        t.CheckOrthonormal();
        var w = t.Apply(CR) - CR;
        Assert.True(w.X > 0);
        Assert.Equal(0, w.Y, 6);
        Assert.Equal(0, w.Z, 6);
        var b = t.Apply(CL) - CL;
        Assert.Equal(4, b.Length, 6);
        Assert.Equal(15, Deg(Math.Atan2(b.X, b.Y)), 6);     // Беннет в горизонтальной плоскости, медиально (+X)
        Assert.Equal(40, Deg(Math.Atan2(-b.Z, b.Y)), 6);    // ССП в сагиттальной проекции
        var d = t.Apply(Incisal) - Incisal;
        Assert.Equal(35, Deg(Math.Atan2(-d.Z, Math.Sqrt(d.X * d.X + d.Y * d.Y))), 6);
        Assert.Equal(Vec3.Distance(CR, CL), Vec3.Distance(t.Apply(CR), t.Apply(CL)), 9);
    }

    [Fact]
    public void LaterotrusionLeft_IsMirrorOfRight()
    {
        var k = Kin();
        var r = k.Laterotrusion(Side.Right, 3).Apply(new Vec3(25, 40, -35));
        var l = k.Laterotrusion(Side.Left, 3).Apply(new Vec3(-25, 40, -35));
        Assert.Equal(r.X, -l.X, 6);
        Assert.Equal(r.Y, l.Y, 6);
        Assert.Equal(r.Z, l.Z, 6);
    }

    [Fact]
    public void Retrusion_IsClampedByLongCentric()
    {
        var t = Kin(lc: 0.5).Retrusion(2);
        Assert.Equal(new Vec3(0, -0.5, 0), t.Apply(Vec3.Zero));
    }

    [Fact]
    public void PathAngle_Protrusive_BetweenCondyleAndIncisor()
    {
        var k = Kin(a3: 60);
        double nearCondyle = k.PathAngle(Movement.Protrusion, new Vec3(40, 5, -5));
        double molar = k.PathAngle(Movement.Protrusion, new Vec3(25, 45, -35));
        double incisor = k.PathAngle(Movement.Protrusion, Incisal);
        Assert.InRange(nearCondyle, 44, 48);
        Assert.InRange(molar, 46, 59);
        Assert.Equal(60, incisor, 3);
    }

    [Fact]
    public void Horn_RecoversKnownTransform()
    {
        var r = Rotation.FromRotationVector(new Vec3(0.1, -0.2, 0.3));
        var t = new RigidTransform(r, new Vec3(1, 2, 3));
        var src = new[] { new Vec3(0, 0, 0), new Vec3(10, 0, 0), new Vec3(0, 10, 0), new Vec3(3, 4, 12) };
        var fit = Rotation.Horn(src, src.Select(t.Apply).ToArray());
        foreach (var p in src) Assert.True(Vec3.Distance(t.Apply(p), fit.Apply(p)) < 1e-9);
    }
}

public class ContactCheckTests
{
    /// <summary>Верхняя «пластина» z = f(x, y) с нормалями вниз (к нижней челюсти).</summary>
    private static Mesh Plate(Func<double, double, double> z)
    {
        var m = new Mesh();
        int nx = 61, ny = 61;
        for (int j = 0; j < ny; j++)
            for (int i = 0; i < nx; i++)
            {
                double x = -60 + 2.0 * i, y = -10 + 2.0 * j;
                m.AddVertex(new Vec3(x, y, z(x, y)));
            }
        for (int j = 0; j < ny - 1; j++)
            for (int i = 0; i < nx - 1; i++)
            {
                int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                m.AddTriangle(a, c, d); // нормаль −Z
                m.AddTriangle(a, d, b);
            }
        return m;
    }

    private static Mesh Points(params Vec3[] p)
    {
        var m = new Mesh();
        foreach (var v in p) m.AddVertex(v);
        return m;
    }

    private static readonly ArchZones Zones = new(60, 60, 31.5, 31.5);

    [Fact]
    public void ProximityGrid_SignIsPositiveBelowPlate()
    {
        var g = new ProximityGrid(Plate((_, _) => 0));
        Assert.Equal(0.3, g.SignedDistance(new Vec3(1, 1, -0.3), 1)!.Value, 9);
        Assert.Equal(-0.2, g.SignedDistance(new Vec3(1, 1, 0.2), 1)!.Value, 9);
        Assert.Null(g.SignedDistance(new Vec3(1, 1, -3), 1));
    }

    [Fact]
    public void FlatPlate_DisoccludesInAllExcursions()
    {
        var upper = Plate((_, _) => 0);
        var lower = Points(new Vec3(25, 45, -0.05), new Vec3(-25, 45, -0.05), new Vec3(5, 80, -0.05), new Vec3(26, 25, -0.05));
        var rep = ContactCheck.Run(upper, lower, KinematicsTests.Kin(), Zones, 0.5);
        Assert.True(rep.Verdicts.Single(v => v.Movement == Movement.Protrusion).Ok);
        Assert.All(rep.Verdicts.Where(v => v.Text.Contains("балансирующей")), v => Assert.True(v.Ok));
        // Плоскость не даёт рабочих контактов — групповая функция не подтверждается.
        Assert.Contains(rep.Verdicts, v => v.Text.Contains("групповая функция не подтверждена"));
        Assert.True(rep.Verdicts.Single(v => v.Movement == Movement.Retrusion).Ok);
    }

    [Fact]
    public void SlopeDescendingBackwards_GivesRetrusiveInterference()
    {
        // Верхний скат опускается кзади: при сдвиге нижней кзади — проникновение.
        var upper = Plate((_, y) => 0.3 * y);
        var lower = Points(new Vec3(25, 45, 0.3 * 45 - 0.02));
        var rep = ContactCheck.Run(upper, lower, KinematicsTests.Kin(), Zones, 0.5);
        Assert.False(rep.Verdicts.Single(v => v.Movement == Movement.Retrusion).Ok);
    }

    [Fact]
    public void Zones_ClassifyTeeth()
    {
        Assert.Equal(ToothRegion.AnteriorRight, Zones.Classify(new Vec3(10, 70, 0)));
        Assert.Equal(ToothRegion.PosteriorLeft, Zones.Classify(new Vec3(-25, 45, 0)));
        Assert.Equal(ToothRegion.SecondMolarRight, Zones.Classify(new Vec3(27, 25, 0)));
    }
}

public class ProximityPerformanceTests
{
    [Fact]
    public void LargeMesh_ContactCheck_FinishesQuickly()
    {
        // ~180 тыс. треугольников верхней и ~90 тыс. вершин нижней — порядок реального скана.
        int n = 300;
        var upper = new Mesh();
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                upper.AddVertex(new Vec3(-30 + 60.0 * i / (n - 1), 20 + 70.0 * j / (n - 1), 0.5 * Math.Sin(i * 0.3) * Math.Cos(j * 0.3)));
        for (int j = 0; j < n - 1; j++)
            for (int i = 0; i < n - 1; i++)
            {
                int a = j * n + i, b = a + 1, c = a + n, d = c + 1;
                upper.AddTriangle(a, c, d);
                upper.AddTriangle(a, d, b);
            }
        var lower = new Mesh();
        for (int j = 0; j < n; j++)
            for (int i = 0; i < n; i++)
                lower.AddVertex(new Vec3(-30 + 60.0 * i / (n - 1), 20 + 70.0 * j / (n - 1), -0.6 - 3.0 * ((i * 7 + j * 13) % 10) / 10.0));
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var rep = ContactCheck.Run(upper, lower, KinematicsTests.Kin(), new ArchZones(60, 60, 31.5, 31.5), 0.5);
        sw.Stop();
        Assert.NotEmpty(rep.Steps);
        Assert.True(sw.Elapsed.TotalSeconds < 30, $"Проверка заняла {sw.Elapsed.TotalSeconds:0.0} с");
    }
}
