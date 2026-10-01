using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;
using Kuwata.Core.IO;
using Kuwata.Core.Method;

namespace Kuwata.Core.Tests;

public class GeometryTests
{
    private static void Near(Vec3 a, Vec3 b, double tol = 1e-6) =>
        Assert.True(Vec3.Distance(a, b) < tol, $"{a} != {b}");

    [Fact]
    public void AxisOrbital_Frame_PutsCondylesOnXAxis()
    {
        // Произвольный поворот + сдвиг «мира».
        var cr = new Vec3(60, 10, 5);
        var cl = new Vec3(-50, 12, 3);
        var or = new Vec3(30, 85, 30);
        var t = ArticulatorFrame.FromAxisOrbital(cr, cl, or);
        var a = t.Apply(cr);
        var b = t.Apply(cl);
        Assert.True(a.X > 0 && Math.Abs(a.Y) < 1e-9 && Math.Abs(a.Z) < 1e-9);
        Assert.True(b.X < 0 && Math.Abs(b.Y) < 1e-9 && Math.Abs(b.Z) < 1e-9);
        Assert.Equal(-a.X, b.X, 9);
        var o = t.Apply(or);
        Assert.True(o.Y > 0);
        Assert.Equal(0, o.Z, 9);
    }

    [Fact]
    public void AverageValues_PlaceIncisorOnBonwillTriangle()
    {
        var inc = new Vec3(3, 7, -2);
        var m6r = new Vec3(27, -28, 1);
        var m6l = new Vec3(-21, -28, 1);
        var t = ArticulatorFrame.FromAverageValues(inc, m6r, m6l);
        var i = t.Apply(inc);
        Assert.Equal(110, Vec3.Distance(i, new Vec3(55, 0, 0)), 6);
        Assert.Equal(110, Vec3.Distance(i, new Vec3(-55, 0, 0)), 6);
        // Треугольник Бонвиля наклонён на 22° + 10° = 32° к горизонтали.
        Assert.Equal(32, Math.Atan2(-i.Z, i.Y) * 180 / Math.PI, 6);
        // Окклюзионная плоскость опускается кпереди на 10°.
        var mid = (t.Apply(m6r) + t.Apply(m6l)) / 2;
        Assert.Equal(10, Math.Atan2(mid.Z - i.Z, i.Y - mid.Y) * 180 / Math.PI, 6);
        Assert.True(t.Apply(m6r).X > 0);
    }

    [Fact]
    public void RigidTransform_InverseAndMatrixRoundTrip()
    {
        var t = ArticulatorFrame.FromAxisOrbital(new Vec3(60, 10, 5), new Vec3(-50, 12, 3), new Vec3(30, 85, 30));
        var p = new Vec3(1, 2, 3);
        Near(p, t.Inverse().Apply(t.Apply(p)));
        var t2 = RigidTransform.FromRowMajor4x4(t.ToRowMajor4x4());
        Near(t.Apply(p), t2.Apply(p));
    }

    [Fact]
    public void SphereFit_RecoversCondyleCenter()
    {
        var s = MeshBuilder.Sphere(new Vec3(52, 3, -1), 9, 24, 16);
        var (c, r) = SphereFit.Fit(s.Vertices);
        Near(c, new Vec3(52, 3, -1), 1e-6);
        Assert.Equal(9, r, 6);
        var (c2, _) = SphereFit.CondyleHead(s, 6);
        Near(c2, new Vec3(52, 3, -1), 1e-6);
    }

    [Fact]
    public void Stl_BinaryRoundTrip_And_Ascii()
    {
        var m = MeshBuilder.Sphere(Vec3.Zero, 5);
        using var ms = new MemoryStream();
        StlIO.WriteBinary(ms, m);
        ms.Position = 0;
        var back = StlIO.Read(ms);
        Assert.Equal(m.TriangleCount, back.TriangleCount);

        var ascii = "solid t\nfacet normal 0 0 1\nouter loop\nvertex 0 0 0\nvertex 1 0 0\nvertex 0 1 0\nendloop\nendfacet\nendsolid t\n";
        var a = StlIO.Read(new MemoryStream(System.Text.Encoding.ASCII.GetBytes(ascii)));
        Assert.Equal(1, a.TriangleCount);
    }

    [Fact]
    public void Obj_ParsesPolygons()
    {
        var m = ObjIO.Parse(["v 0 0 0", "v 1 0 0", "v 1 1 0", "v 0 1 0", "f 1/1 2/2 3/3 4/4"]);
        Assert.Equal(2, m.TriangleCount);
    }

    [Fact]
    public void DetectIncisorTips_OnSyntheticBlocks()
    {
        // Верхний «резец»: край на z = −1, y = 50; нижний: край z = +1, y = 48.
        var up = new Mesh(); up.AddVertex(new Vec3(0, 50, -1)); up.AddVertex(new Vec3(0, 49, 5)); up.AddVertex(new Vec3(20, 30, 0)); up.AddTriangle(0, 1, 2);
        var lo = new Mesh(); lo.AddVertex(new Vec3(0, 48, 1)); lo.AddVertex(new Vec3(0, 47, -6)); lo.AddVertex(new Vec3(20, 28, 0)); lo.AddTriangle(0, 1, 2);
        var (u, l) = Overlap.DetectIncisorTips(up, lo);
        var o = Overlap.FromIncisorTips(u, l);
        Assert.Equal(2, o.Overbite, 6);
        Assert.Equal(2, o.Overjet, 6);
    }
}
