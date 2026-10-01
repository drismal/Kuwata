using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;
using Kuwata.Core.Method;

namespace Kuwata.Core.Tests;

public class BroadrickTests
{
    // Типичные ориентиры нижней дуги в системе артикулятора (мм).
    internal static SideLandmarks Right => new(new Vec3(17, 85, -42), new Vec3(25, 60, -36), new Vec3(27, 42, -33));
    internal static SideLandmarks Left => new(new Vec3(-17, 85, -42), new Vec3(-25, 60, -36), new Vec3(-27, 42, -33));

    [Fact]
    public void BisectingPoint_IsAtRadiusFromBothPoints()
    {
        var s = new BroadrickSettings { OpscShiftMm = 0 };
        var b = BroadrickSide.Build(Side.Right, Right.Asp, Right.Mpsp!.Value, s);
        double R = BroadrickSettings.FourInchesMm;
        Assert.Equal(R, Dist(b.BisectingPoint, b.Asp), 6);
        Assert.Equal(R, Dist(b.BisectingPoint, b.Posterior), 6);
        Assert.True(b.BisectingPoint.Z > b.Asp.Z);
        // Без сдвига кривая проходит через ASP и MPSP.
        Assert.Equal(b.Asp.Z, b.SpeeZ(b.Asp.Y), 6);
        Assert.Equal(b.Posterior.Z, b.SpeeZ(b.Posterior.Y), 6);
    }

    [Fact]
    public void OpscShiftedBackwards_LowersMolars_KeepsAsp()
    {
        // т.1 с.190–191: OPSC кзади от точки деления — бугры нижних моляров ниже.
        var b0 = BroadrickSide.Build(Side.Right, Right.Asp, Right.Mpsp!.Value, new BroadrickSettings { OpscShiftMm = 0 });
        var b2 = BroadrickSide.Build(Side.Right, Right.Asp, Right.Mpsp!.Value, new BroadrickSettings { OpscShiftMm = 2 });
        Assert.True(b2.Opsc.Y < b0.Opsc.Y);
        Assert.Equal(BroadrickSettings.FourInchesMm, Dist(b2.Opsc, b2.Asp), 6);
        Assert.Equal(b2.Asp.Z, b2.SpeeZ(b2.Asp.Y), 6);
        Assert.True(b2.SpeeZ(b2.Posterior.Y) < b0.SpeeZ(b0.Posterior.Y));
    }

    [Fact]
    public void Cpsp_IsInFrontOfCondyle()
    {
        var c = ArticulatorFrame.Cpsp(Side.Left);
        Assert.Equal(-55, c.X);
        Assert.True(c.Y > 0);
        var b = BroadrickSide.Build(Side.Left, Left.Asp, c, new BroadrickSettings());
        Assert.True(b.Opsc.Z > 0); // центр кривой выше зубов
    }

    [Fact]
    public void Wilson_PassesThroughBothMolars_EvenIfAsymmetric()
    {
        var w = new WilsonCurve(101.6, (25, -36), (-25, -34), 60);
        Assert.Equal(-36, w.Z(25), 6);
        Assert.Equal(-34, w.Z(-25), 6);
        Assert.True(w.CenterZ > 0);
        // Вогнута вверх: у средней линии ниже, чем у щёчных бугров.
        Assert.True(w.Z(w.CenterX) < -36);
    }

    [Fact]
    public void Surface_FollowsSpeeOnBuccalLine_AndWilsonInside()
    {
        var sch = OcclusalSurfaceBuilder.Build(Right, Left, new BroadrickSettings(), new SurfaceSettings());
        Assert.True(sch.Surface.TriangleCount > 100);
        // Щёчные бугры первого моляра — на кривой Шпее.
        var m6 = Right.LowerFirstMolar;
        var nearest = sch.Surface.Vertices.MinBy(v => Math.Pow(v.X - m6.X, 2) + Math.Pow(v.Y - m6.Y, 2));
        Assert.Equal(sch.Right.SpeeZ(nearest.Y), nearest.Z, 1);
        // Язычнее щёчной линии поверхность ниже (кривая Уилсона).
        var inner = sch.Surface.Vertices.Where(v => v.X > 0 && Math.Abs(v.Y - nearest.Y) < 1e-6).MinBy(v => v.X);
        Assert.True(inner.Z < nearest.Z);
        Assert.Contains(sch.Curves, c => c.Name.StartsWith("Кривая Уилсона"));
    }

    private static double Dist((double Y, double Z) a, (double Y, double Z) b) =>
        Math.Sqrt((a.Y - b.Y) * (a.Y - b.Y) + (a.Z - b.Z) * (a.Z - b.Z));
}
