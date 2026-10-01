using System.Globalization;

namespace Kuwata.Core.Geometry;

/// <summary>Трёхмерный вектор двойной точности. Единицы — мм.</summary>
public readonly record struct Vec3(double X, double Y, double Z)
{
    public static readonly Vec3 Zero = new(0, 0, 0);
    public static readonly Vec3 UnitX = new(1, 0, 0);
    public static readonly Vec3 UnitY = new(0, 1, 0);
    public static readonly Vec3 UnitZ = new(0, 0, 1);

    public static Vec3 operator +(Vec3 a, Vec3 b) => new(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
    public static Vec3 operator -(Vec3 a, Vec3 b) => new(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
    public static Vec3 operator -(Vec3 a) => new(-a.X, -a.Y, -a.Z);
    public static Vec3 operator *(Vec3 a, double k) => new(a.X * k, a.Y * k, a.Z * k);
    public static Vec3 operator *(double k, Vec3 a) => a * k;
    public static Vec3 operator /(Vec3 a, double k) => new(a.X / k, a.Y / k, a.Z / k);

    public double Dot(Vec3 b) => X * b.X + Y * b.Y + Z * b.Z;
    public Vec3 Cross(Vec3 b) => new(Y * b.Z - Z * b.Y, Z * b.X - X * b.Z, X * b.Y - Y * b.X);
    public double Length => Math.Sqrt(X * X + Y * Y + Z * Z);

    public Vec3 Normalized()
    {
        var l = Length;
        if (l < 1e-12) throw new InvalidOperationException("Нельзя нормировать нулевой вектор.");
        return this / l;
    }

    public static double Distance(Vec3 a, Vec3 b) => (a - b).Length;
    public static Vec3 Lerp(Vec3 a, Vec3 b, double t) => a + (b - a) * t;

    public double[] ToArray() => [X, Y, Z];
    public static Vec3 FromArray(double[] a) => new(a[0], a[1], a[2]);

    public override string ToString() =>
        string.Format(CultureInfo.InvariantCulture, "({0:0.00}; {1:0.00}; {2:0.00})", X, Y, Z);
}
