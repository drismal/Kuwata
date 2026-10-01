namespace Kuwata.Core.Geometry;

/// <summary>
/// Подбор сферы по точкам методом наименьших квадратов (алгебраическая постановка).
/// Используется для центра суставной головки по сегментированному STL.
/// </summary>
public static class SphereFit
{
    public static (Vec3 Center, double Radius) Fit(IReadOnlyList<Vec3> pts)
    {
        if (pts.Count < 4) throw new ArgumentException("Для сферы нужно не меньше 4 точек.");
        // x² + y² + z² = 2ax + 2by + 2cz + d  → линейная система для (a, b, c, d).
        var ata = new double[4, 4];
        var atb = new double[4];
        foreach (var p in pts)
        {
            double[] row = [2 * p.X, 2 * p.Y, 2 * p.Z, 1];
            double rhs = p.Dot(p);
            for (int i = 0; i < 4; i++)
            {
                atb[i] += row[i] * rhs;
                for (int j = 0; j < 4; j++) ata[i, j] += row[i] * row[j];
            }
        }
        var x = LinearAlgebra.Solve(ata, atb);
        var c = new Vec3(x[0], x[1], x[2]);
        double r2 = x[3] + c.Dot(c);
        if (r2 <= 0) throw new InvalidOperationException("Сфера не подбирается по этим точкам.");
        return (c, Math.Sqrt(r2));
    }

    /// <summary>
    /// Центр суставной головки: сфера по вершинам верхней части сетки
    /// (выше maxZ − capHeight в системе координат, где Z — вверх).
    /// </summary>
    public static (Vec3 Center, double Radius) CondyleHead(Mesh condyle, double capHeight = 8.0)
    {
        var (_, max) = condyle.Bounds();
        var cap = condyle.Vertices.Where(v => v.Z >= max.Z - capHeight).ToList();
        return Fit(cap);
    }
}

public static class LinearAlgebra
{
    /// <summary>Решение A·x = b методом Гаусса с выбором главного элемента.</summary>
    public static double[] Solve(double[,] a, double[] b)
    {
        int n = b.Length;
        var m = (double[,])a.Clone();
        var v = (double[])b.Clone();
        for (int col = 0; col < n; col++)
        {
            int piv = col;
            for (int r = col + 1; r < n; r++)
                if (Math.Abs(m[r, col]) > Math.Abs(m[piv, col])) piv = r;
            if (Math.Abs(m[piv, col]) < 1e-12) throw new InvalidOperationException("Вырожденная система.");
            if (piv != col)
            {
                for (int k = 0; k < n; k++) (m[col, k], m[piv, k]) = (m[piv, k], m[col, k]);
                (v[col], v[piv]) = (v[piv], v[col]);
            }
            for (int r = col + 1; r < n; r++)
            {
                double f = m[r, col] / m[col, col];
                for (int k = col; k < n; k++) m[r, k] -= f * m[col, k];
                v[r] -= f * v[col];
            }
        }
        var x = new double[n];
        for (int r = n - 1; r >= 0; r--)
        {
            double s = v[r];
            for (int k = r + 1; k < n; k++) s -= m[r, k] * x[k];
            x[r] = s / m[r, r];
        }
        return x;
    }
}
