namespace Kuwata.Core.Geometry;

/// <summary>
/// Жёсткое преобразование p' = R·p + T. Строки R — образы осей целевой системы
/// в исходной системе (для преобразований, построенных из ортонормированного репера).
/// </summary>
public sealed class RigidTransform
{
    // Матрица поворота по строкам.
    public double[,] R { get; }
    public Vec3 T { get; }

    public RigidTransform(double[,] r, Vec3 t)
    {
        if (r.GetLength(0) != 3 || r.GetLength(1) != 3) throw new ArgumentException("R должна быть 3×3.");
        R = (double[,])r.Clone();
        T = t;
    }

    public static RigidTransform Identity => new(new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } }, Vec3.Zero);

    public Vec3 Apply(Vec3 p) => ApplyRotation(p) + T;

    public Vec3 ApplyRotation(Vec3 v) => new(
        R[0, 0] * v.X + R[0, 1] * v.Y + R[0, 2] * v.Z,
        R[1, 0] * v.X + R[1, 1] * v.Y + R[1, 2] * v.Z,
        R[2, 0] * v.X + R[2, 1] * v.Y + R[2, 2] * v.Z);

    public RigidTransform Inverse()
    {
        var rt = new double[3, 3];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                rt[i, j] = R[j, i];
        var inv = new RigidTransform(rt, Vec3.Zero);
        return new RigidTransform(rt, -inv.ApplyRotation(T));
    }

    /// <summary>Композиция: сначала <paramref name="first"/>, затем this.</summary>
    public RigidTransform After(RigidTransform first)
    {
        var r = new double[3, 3];
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
                r[i, j] = R[i, 0] * first.R[0, j] + R[i, 1] * first.R[1, j] + R[i, 2] * first.R[2, j];
        return new RigidTransform(r, ApplyRotation(first.T) + T);
    }

    /// <summary>
    /// Преобразование из мировой системы в локальную систему репера
    /// (origin, ортонормированные оси ex, ey, ez, заданные в мировых координатах).
    /// </summary>
    public static RigidTransform WorldToFrame(Vec3 origin, Vec3 ex, Vec3 ey, Vec3 ez)
    {
        var r = new double[,] { { ex.X, ex.Y, ex.Z }, { ey.X, ey.Y, ey.Z }, { ez.X, ez.Y, ez.Z } };
        var tr = new RigidTransform(r, Vec3.Zero);
        return new RigidTransform(r, -tr.ApplyRotation(origin));
    }

    /// <summary>Матрица 4×4 по строкам (16 чисел), последняя строка 0 0 0 1.</summary>
    public double[] ToRowMajor4x4() =>
    [
        R[0, 0], R[0, 1], R[0, 2], T.X,
        R[1, 0], R[1, 1], R[1, 2], T.Y,
        R[2, 0], R[2, 1], R[2, 2], T.Z,
        0, 0, 0, 1,
    ];

    public static RigidTransform FromRowMajor4x4(double[] m)
    {
        if (m.Length != 16) throw new ArgumentException("Нужно 16 чисел матрицы 4×4.");
        var r = new double[,] { { m[0], m[1], m[2] }, { m[4], m[5], m[6] }, { m[8], m[9], m[10] } };
        var t = new RigidTransform(r, new Vec3(m[3], m[7], m[11]));
        t.CheckOrthonormal();
        return t;
    }

    public void CheckOrthonormal(double tol = 1e-4)
    {
        for (int i = 0; i < 3; i++)
            for (int j = 0; j < 3; j++)
            {
                double d = R[i, 0] * R[j, 0] + R[i, 1] * R[j, 1] + R[i, 2] * R[j, 2];
                if (Math.Abs(d - (i == j ? 1 : 0)) > tol)
                    throw new ArgumentException("Матрица поворота не ортонормирована — преобразование не жёсткое.");
            }
        double det =
            R[0, 0] * (R[1, 1] * R[2, 2] - R[1, 2] * R[2, 1]) -
            R[0, 1] * (R[1, 0] * R[2, 2] - R[1, 2] * R[2, 0]) +
            R[0, 2] * (R[1, 0] * R[2, 1] - R[1, 1] * R[2, 0]);
        if (det < 0) throw new ArgumentException("Матрица содержит отражение (det < 0).");
    }
}
