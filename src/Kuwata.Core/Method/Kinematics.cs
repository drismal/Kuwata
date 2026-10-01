using Kuwata.Core.Articulator;
using Kuwata.Core.Geometry;

namespace Kuwata.Core.Method;

public enum Movement { Protrusion, LaterotrusionRight, LaterotrusionLeft, Retrusion }

public sealed class KinematicsParameters
{
    public double SagittalPathRight { get; init; }
    public double SagittalPathLeft { get; init; }
    public double BennettRight { get; init; }
    public double BennettLeft { get; init; }
    public required GuidanceAngles Guidance { get; init; }
    /// <summary>Резцовая точка (режущий край нижнего центрального резца) в системе артикулятора.</summary>
    public Vec3 IncisalPoint { get; init; }
    public double LongCentricMm { get; init; } = 0.5;
}

/// <summary>
/// Кинематика нижней челюсти в артикуляторе (docs/KUWATA_METHOD.md, раздел 11).
/// Возвращает жёсткое преобразование нижней челюсти в системе артикулятора; верхняя неподвижна.
/// Параметр движения:
///   протрузия и латеротрузия — путь мыщелка (балансирующего при латеротрузии), мм;
///   ретрузия — сдвиг кзади, мм (не больше long centric).
/// </summary>
public sealed class ArticulatorKinematics
{
    private readonly KinematicsParameters _p;
    private readonly Vec3 _cr, _cl;

    public ArticulatorKinematics(KinematicsParameters p, ArtexCrConfig? artex = null)
    {
        _p = p;
        _cr = ArticulatorFrame.CondyleCenter(Side.Right, artex);
        _cl = ArticulatorFrame.CondyleCenter(Side.Left, artex);
    }

    public RigidTransform Pose(Movement m, double amount) => m switch
    {
        Movement.Protrusion => Protrusion(amount),
        Movement.LaterotrusionRight => Laterotrusion(Side.Right, amount),
        Movement.LaterotrusionLeft => Laterotrusion(Side.Left, amount),
        Movement.Retrusion => Retrusion(amount),
        _ => throw new ArgumentOutOfRangeException(nameof(m)),
    };

    private static double Rad(double d) => d * Math.PI / 180;

    /// <summary>
    /// Протрузия: мыщелки идут по ССП своей стороны, резцовая точка — по A3.
    /// Положение подбирается методом Хорна по трём точкам.
    /// </summary>
    public RigidTransform Protrusion(double s)
    {
        if (s <= 0) return RigidTransform.Identity;
        var dR = new Vec3(0, Math.Cos(Rad(_p.SagittalPathRight)), -Math.Sin(Rad(_p.SagittalPathRight)));
        var dL = new Vec3(0, Math.Cos(Rad(_p.SagittalPathLeft)), -Math.Sin(Rad(_p.SagittalPathLeft)));
        var cr = _cr + dR * s;
        var cl = _cl + dL * s;
        // Резцовая точка — по направлению A3, на расстоянии, сохраняющем её удаление от середины мыщелков.
        var a = Rad(_p.Guidance.A3);
        var di = new Vec3(0, Math.Cos(a), -Math.Sin(a));
        var mid0 = (_cr + _cl) / 2;
        var mid = (cr + cl) / 2;
        double r0 = (_p.IncisalPoint - mid0).Length;
        var w = _p.IncisalPoint - mid;
        // |w + u·di| = r0 → u² + 2(w·di)u + (|w|² − r0²) = 0, берём корень у нуля сдвига резца ~ s.
        double b = w.Dot(di), c = w.Dot(w) - r0 * r0;
        double disc = b * b - c;
        if (disc < 0) throw new InvalidOperationException("Протрузия: нет согласованного положения резца.");
        double u = -b + Math.Sqrt(disc);
        var inc = _p.IncisalPoint + di * u;
        return Rotation.Horn([_cr, _cl, _p.IncisalPoint], [cr, cl, inc]);
    }

    /// <summary>
    /// Латеротрузия (RC = 0): рабочий мыщелок смещается только латерально;
    /// балансирующий идёт по направлению (Беннет в горизонтальной, ССП в сагиттальной плоскости);
    /// резцовая точка опускается по A1/A2 относительно горизонтального смещения.
    /// Неизвестные — вектор поворота (3) и латеральный сдвиг (1); метод Ньютона.
    /// </summary>
    public RigidTransform Laterotrusion(Side working, double s)
    {
        if (s <= 0) return RigidTransform.Identity;
        bool right = working == Side.Right;
        var cw = right ? _cr : _cl;
        var cb = right ? _cl : _cr;
        double ssp = right ? _p.SagittalPathLeft : _p.SagittalPathRight;
        double bennett = right ? _p.BennettLeft : _p.BennettRight;
        double sigma = right ? 1 : -1; // направление движения челюсти по X
        // Балансирующий мыщелок: вперёд, медиально (в сторону движения), вниз.
        var dB = new Vec3(sigma * Math.Tan(Rad(bennett)), 1, -Math.Tan(Rad(ssp))).Normalized();
        var target = cb + dB * s;
        double tanA = Math.Tan(Rad(right ? _p.Guidance.A1 : _p.Guidance.A2));
        var inc = _p.IncisalPoint;

        RigidTransform Make(double[] q)
        {
            var r = Rotation.FromRotationVector(new Vec3(q[0], q[1], q[2]));
            var rot = new RigidTransform(r, Vec3.Zero);
            var t = cw + new Vec3(sigma * q[3], 0, 0) - rot.ApplyRotation(cw);
            return new RigidTransform(r, t);
        }

        double[] F(double[] q)
        {
            var m = Make(q);
            var b = m.Apply(cb) - target;
            var d = m.Apply(inc) - inc;
            double horiz = Math.Sqrt(d.X * d.X + d.Y * d.Y);
            return [b.X, b.Y, b.Z, d.Z + tanA * horiz];
        }

        var x = new double[4];
        // Продолжение по параметру: малыми шагами от s = 0 для устойчивости.
        int steps = Math.Max(1, (int)Math.Ceiling(s / 0.5));
        for (int k = 1; k <= steps; k++)
        {
            var tk = cb + dB * (s * k / steps);
            target = tk;
            for (int it = 0; it < 50; it++)
            {
                var f = F(x);
                double norm = Math.Sqrt(f.Sum(v => v * v));
                if (norm < 1e-10) break;
                var jac = new double[4, 4];
                const double h = 1e-7;
                for (int j = 0; j < 4; j++)
                {
                    var xp = (double[])x.Clone();
                    xp[j] += h;
                    var fp = F(xp);
                    for (int i = 0; i < 4; i++) jac[i, j] = (fp[i] - f[i]) / h;
                }
                var dx = LinearAlgebra.Solve(jac, f.Select(v => -v).ToArray());
                for (int j = 0; j < 4; j++) x[j] += dx[j];
            }
        }
        var res = F(x);
        if (Math.Sqrt(res.Sum(v => v * v)) > 1e-6)
            throw new InvalidOperationException("Латеротрузия: решение не сошлось.");
        return Make(x);
    }

    /// <summary>Ретрузия: горизонтальный сдвиг кзади, ограниченный long centric.</summary>
    public RigidTransform Retrusion(double r)
    {
        double d = Math.Clamp(r, 0, _p.LongCentricMm);
        return new RigidTransform(RigidTransform.Identity.R, new Vec3(0, -d, 0));
    }

    /// <summary>
    /// Наклон траектории точки (от горизонтали артикулятора), °:
    /// протрузия — в сагиттальной проекции, латеротрузия — во фронтальной.
    /// Это «динамический» аналог P3 / WP / BP Куваты.
    /// </summary>
    public double PathAngle(Movement m, Vec3 point, double amount = 1.0)
    {
        var d = Pose(m, amount).Apply(point) - point;
        double horiz = m switch
        {
            Movement.Protrusion or Movement.Retrusion => Math.Abs(d.Y),
            _ => Math.Abs(d.X),
        };
        return Math.Atan2(-d.Z, horiz) * 180 / Math.PI;
    }
}
