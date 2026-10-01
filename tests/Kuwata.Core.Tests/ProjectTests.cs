using Kuwata.Core.Geometry;
using Kuwata.Core.Method;
using Kuwata.Core.Project;

namespace Kuwata.Core.Tests;

public class ProjectTests
{
    private static KuwataProject SyntheticCase()
    {
        var p = new KuwataProject { CaseName = "Тест", Registration = RegistrationMode.AlreadyInArticulator };
        p.SetLandmark(Landmark.AspRight, BroadrickTests.Right.Asp);
        p.SetLandmark(Landmark.LowerFirstMolarRight, BroadrickTests.Right.LowerFirstMolar);
        p.SetLandmark(Landmark.MpspRight, BroadrickTests.Right.Mpsp!.Value);
        p.SetLandmark(Landmark.AspLeft, BroadrickTests.Left.Asp);
        p.SetLandmark(Landmark.LowerFirstMolarLeft, BroadrickTests.Left.LowerFirstMolar);
        p.SetLandmark(Landmark.MpspLeft, BroadrickTests.Left.Mpsp!.Value);
        p.SetLandmark(Landmark.UpperIncisorTip, new Vec3(0, 92, -40));
        p.SetLandmark(Landmark.LowerIncisorTip, new Vec3(0, 90, -38));
        p.Manual.A1 = 40;
        p.Manual.A2 = 40;
        p.Meshes[MeshRole.Lower] = MeshBuilder.Sphere(new Vec3(0, 60, -40), 5);
        return p;
    }

    [Fact]
    public void FullCase_ComputesEverything()
    {
        var p = SyntheticCase();
        var r = CaseCalculator.Compute(p);
        Assert.Empty(r.Errors);
        Assert.Equal(45, r.A3.Value!.Value, 6);       // OB 2, OJ 2
        Assert.Equal(ValueSource.Landmarks, r.A3.Source);
        Assert.Equal(117.5, r.Angles!.Right.DeploymentAngle, 6);
        Assert.NotNull(r.Scheme);
        Assert.NotNull(r.Artex);
        var text = Report.Build(p, r);
        Assert.Contains("OPSC", text);
        Assert.Contains("Ханау", text);
    }

    [Fact]
    public void MissingLandmarks_ReportedAsErrors_NotThrown()
    {
        var r = CaseCalculator.Compute(new KuwataProject());
        Assert.NotEmpty(r.Errors);
        Assert.Null(r.Scheme);
    }

    [Fact]
    public void ProjectFile_RoundTrip()
    {
        var p = SyntheticCase();
        p.Joint.BennettLeft = 15;
        p.Broadrick.PosteriorPoint = PosteriorSurveyPoint.Cpsp;
        var path = Path.Combine(Path.GetTempPath(), $"kw_{Guid.NewGuid():N}.kwp");
        try
        {
            ProjectFile.Save(p, path);
            var q = ProjectFile.Load(path);
            Assert.Equal("Тест", q.CaseName);
            Assert.Equal(15, q.Joint.BennettLeft);
            Assert.Null(q.Joint.BennettRight);
            Assert.Equal(PosteriorSurveyPoint.Cpsp, q.Broadrick.PosteriorPoint);
            Assert.Equal(p.GetLandmark(Landmark.AspRight), q.GetLandmark(Landmark.AspRight));
            Assert.Equal(p.Meshes[MeshRole.Lower].TriangleCount, q.Meshes[MeshRole.Lower].TriangleCount);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void Export_ToOriginalScans_UsesInverseTransform()
    {
        var p = SyntheticCase();
        // Сдвигаем «мир»: сканы не в системе артикулятора, а смещены на +10 по X.
        p.Registration = RegistrationMode.FacebowMatrix;
        p.FacebowMatrix = [1, 0, 0, -10, 0, 1, 0, 0, 0, 0, 1, 0, 0, 0, 0, 1];
        foreach (var k in p.Landmarks.Keys.ToList())
        {
            var v = p.GetLandmark(k)!.Value;
            p.SetLandmark(k, v + new Vec3(10, 0, 0));
        }
        var r = CaseCalculator.Compute(p);
        Assert.Empty(r.Errors);
        var art = Export.SurfaceMesh(r, ExportFrame.Articulator, false);
        var scan = Export.SurfaceMesh(r, ExportFrame.OriginalScans, false);
        Assert.Equal(art.Vertices[0].X + 10, scan.Vertices[0].X, 6);
    }
}
