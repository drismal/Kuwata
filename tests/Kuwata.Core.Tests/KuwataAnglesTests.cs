using Kuwata.Core.Method;

namespace Kuwata.Core.Tests;

public class KuwataAnglesTests
{
    [Fact]
    public void HanauBennett_Example_FromBook()
    {
        // т.1 с.136: ССП 45° → 45/8 + 12 = 17.6°
        Assert.Equal(17.625, KuwataAngles.HanauBennett(45), 3);
    }

    [Fact]
    public void ControlExample_Matches_HandCalculation()
    {
        var r = KuwataAngles.Calculate(
            new JointParameters { SagittalPathRight = 45, SagittalPathLeft = 45 },
            new GuidanceAngles(40, 40, 45));

        Assert.Equal(20, r.Right.WorkingSlope, 6);
        Assert.Equal(42.5, r.Right.BalancingSlope, 6);
        Assert.Equal(45, r.Right.ProtrusiveSlope, 6);
        Assert.Equal(117.5, r.Right.DeploymentAngle, 6);
        Assert.Equal(new[] { 37.5, 35.5, 33.5, 31.5 }, r.Right.Teeth.Select(t => t.Tolerance));
        Assert.True(r.BennettRightFromHanau);
        Assert.Equal(17.625, r.BennettRight, 3);
    }

    [Fact]
    public void Formulas_UseCorrectSides()
    {
        // A1 — движение вправо: рабочая правая (WP1), балансирующая левая (BP2 = (A1 + C2)/2).
        var r = KuwataAngles.Calculate(
            new JointParameters { SagittalPathRight = 30, SagittalPathLeft = 50, BennettRight = 10, BennettLeft = 12 },
            new GuidanceAngles(A1: 20, A2: 40, A3: 36));
        Assert.Equal(10, r.Right.WorkingSlope, 6);           // WP1 = A1/2
        Assert.Equal(20, r.Left.WorkingSlope, 6);            // WP2 = A2/2
        Assert.Equal((20 + 50) / 2.0, r.Left.BalancingSlope, 6);  // BP2 = (A1 + C2)/2
        Assert.Equal((40 + 30) / 2.0, r.Right.BalancingSlope, 6); // BP1 = (A2 + C1)/2
        Assert.Equal((36 + 30) / 2.0, r.Right.ProtrusiveSlope, 6);
        Assert.Equal((36 + 50) / 2.0, r.Left.ProtrusiveSlope, 6);
        Assert.Equal(180 - (10 + 35), r.Right.DeploymentAngle, 6);
        Assert.False(r.BennettRightFromHanau);
        Assert.Equal(10, r.BennettRight);
    }

    [Fact]
    public void Warnings_ForOutOfRangeGuidance_AndFlatCusps()
    {
        var r = KuwataAngles.Calculate(new JointParameters { SagittalPathRight = 20, SagittalPathLeft = 20 },
            new GuidanceAngles(10, 30, 0));
        Assert.Contains(r.Warnings, w => w.StartsWith("A1"));
        Assert.Contains(r.Warnings, w => w.StartsWith("A3"));
        Assert.Contains(r.Warnings, w => w.Contains("почти плоские"));
    }

    [Theory]
    [InlineData(2, 2, 45)]
    [InlineData(0, 3, 0)]
    [InlineData(3, 0, 90)]
    public void GuidanceAngle_FromOverlap(double ob, double oj, double expected)
    {
        Assert.Equal(expected, Overlap.GuidanceAngle(new OverlapValues(ob, oj)), 6);
    }

    [Fact]
    public void ToothProportions_ScaleFromIncisor()
    {
        var t = ToothProportions.Calculate(9.35, 11.55); // k = 1.1
        var u6 = t.Single(x => x.Upper && x.Tooth == 6);
        Assert.Equal(11.0, u6.MesioDistal, 6);
        Assert.Equal(8.25, u6.CrownHeight, 6);
        Assert.Equal(12.1 * 0.55, u6.CuspDistanceMin!.Value, 6);
        Assert.Null(u6.ApparentWidth);

        var g = ToothProportions.Calculate(8.5, 10.5, ProportionSystem.RickettsGolden);
        Assert.Equal(8.5 * 0.618, g.Single(x => x.Upper && x.Tooth == 2).ApparentWidth!.Value, 6);
    }
}
