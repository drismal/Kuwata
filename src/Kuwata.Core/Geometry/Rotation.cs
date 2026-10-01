namespace Kuwata.Core.Geometry;

public static class Rotation
{
    /// <summary>Матрица поворота по вектору поворота (ось × угол, рад) — формула Родрига.</summary>
    public static double[,] FromRotationVector(Vec3 r)
    {
        double th = r.Length;
        if (th < 1e-15) return new double[,] { { 1, 0, 0 }, { 0, 1, 0 }, { 0, 0, 1 } };
        var k = r / th;
        double c = Math.Cos(th), s = Math.Sin(th), v = 1 - c;
        return new double[,]
        {
            { c + k.X * k.X * v,       k.X * k.Y * v - k.Z * s, k.X * k.Z * v + k.Y * s },
            { k.Y * k.X * v + k.Z * s, c + k.Y * k.Y * v,       k.Y * k.Z * v - k.X * s },
            { k.Z * k.X * v - k.Y * s, k.Z * k.Y * v + k.X * s, c + k.Z * k.Z * v },
        };
    }

    /// <summary>
    /// Метод Хорна (кватернионы): жёсткое преобразование, наилучшим образом
    /// (МНК) переводящее точки src в dst.
    /// </summary>
    public static RigidTransform Horn(IReadOnlyList<Vec3> src, IReadOnlyList<Vec3> dst)
    {
        if (src.Count != dst.Count || src.Count < 3) throw new ArgumentException("Нужно ≥ 3 пар точек.");
        var cs = Centroid(src);
        var cd = Centroid(dst);
        double sxx = 0, sxy = 0, sxz = 0, syx = 0, syy = 0, syz = 0, szx = 0, szy = 0, szz = 0;
        for (int i = 0; i < src.Count; i++)
        {
            var a = src[i] - cs;
            var b = dst[i] - cd;
            sxx += a.X * b.X; sxy += a.X * b.Y; sxz += a.X * b.Z;
            syx += a.Y * b.X; syy += a.Y * b.Y; syz += a.Y * b.Z;
            szx += a.Z * b.X; szy += a.Z * b.Y; szz += a.Z * b.Z;
        }
        var n = new double[,]
        {
            { sxx + syy + szz, syz - szy,        szx - sxz,        sxy - syx },
            { syz - szy,       sxx - syy - szz,  sxy + syx,        szx + sxz },
            { szx - sxz,       sxy + syx,        -sxx + syy - szz, syz + szy },
            { sxy - syx,       szx + sxz,        syz + szy,        -sxx - syy + szz },
        };
        var q = LargestEigenvector(n);
        double w = q[0], x = q[1], y = q[2], z = q[3];
        var r = new double[,]
        {
            { w * w + x * x - y * y - z * z, 2 * (x * y - w * z),           2 * (x * z + w * y) },
            { 2 * (y * x + w * z),           w * w - x * x + y * y - z * z, 2 * (y * z - w * x) },
            { 2 * (z * x - w * y),           2 * (z * y + w * x),           w * w - x * x - y * y + z * z },
        };
        var rot = new RigidTransform(r, Vec3.Zero);
        return new RigidTransform(r, cd - rot.ApplyRotation(cs));
    }

    private static Vec3 Centroid(IReadOnlyList<Vec3> p)
    {
        var s = Vec3.Zero;
        foreach (var v in p) s += v;
        return s / p.Count;
    }

    /// <summary>Собственный вектор симметричной 4×4 с наибольшим собственным числом (метод Якоби).</summary>
    private static double[] LargestEigenvector(double[,] m)
    {
        const int n = 4;
        var a = (double[,])m.Clone();
        var v = new double[n, n];
        for (int i = 0; i < n; i++) v[i, i] = 1;
        for (int sweep = 0; sweep < 100; sweep++)
        {
            double off = 0;
            for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++) off += a[p, q] * a[p, q];
            if (off < 1e-24) break;
            for (int p = 0; p < n; p++)
                for (int q = p + 1; q < n; q++)
                {
                    if (Math.Abs(a[p, q]) < 1e-300) continue;
                    double theta = (a[q, q] - a[p, p]) / (2 * a[p, q]);
                    double t = Math.Sign(theta) / (Math.Abs(theta) + Math.Sqrt(theta * theta + 1));
                    if (theta == 0) t = 1;
                    double c = 1 / Math.Sqrt(t * t + 1), s = t * c;
                    for (int k = 0; k < n; k++)
                    {
                        double akp = a[k, p], akq = a[k, q];
                        a[k, p] = c * akp - s * akq;
                        a[k, q] = s * akp + c * akq;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double apk = a[p, k], aqk = a[q, k];
                        a[p, k] = c * apk - s * aqk;
                        a[q, k] = s * apk + c * aqk;
                    }
                    for (int k = 0; k < n; k++)
                    {
                        double vkp = v[k, p], vkq = v[k, q];
                        v[k, p] = c * vkp - s * vkq;
                        v[k, q] = s * vkp + c * vkq;
                    }
                }
        }
        int best = 0;
        for (int i = 1; i < n; i++) if (a[i, i] > a[best, best]) best = i;
        var e = new double[n];
        double norm = 0;
        for (int i = 0; i < n; i++) { e[i] = v[i, best]; norm += e[i] * e[i]; }
        norm = Math.Sqrt(norm);
        for (int i = 0; i < n; i++) e[i] /= norm;
        return e;
    }
}
