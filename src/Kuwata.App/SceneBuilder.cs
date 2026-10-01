using System.Windows.Media;
using System.Windows.Media.Media3D;
using HelixToolkit.Wpf;
using Kuwata.Core.Geometry;
using KMesh = Kuwata.Core.Geometry.Mesh;

namespace Kuwata.App;

/// <summary>Преобразование данных ядра в объекты WPF 3D.</summary>
internal static class SceneBuilder
{
    public static Point3D P(Vec3 v) => new(v.X, v.Y, v.Z);
    public static Vec3 V(Point3D p) => new(p.X, p.Y, p.Z);

    public static MeshGeometry3D ToGeometry(KMesh m)
    {
        var g = new MeshGeometry3D
        {
            Positions = new Point3DCollection(m.Vertices.Select(P)),
            TriangleIndices = new Int32Collection(m.Indices),
        };
        g.Freeze();
        return g;
    }

    public static ModelVisual3D MeshVisual(KMesh m, Color color, double opacity = 1.0, bool twoSided = false)
    {
        var brush = new SolidColorBrush(color) { Opacity = opacity };
        brush.Freeze();
        var mat = new MaterialGroup();
        mat.Children.Add(new DiffuseMaterial(brush));
        mat.Children.Add(new SpecularMaterial(Brushes.White, 40));
        mat.Freeze();
        var model = new GeometryModel3D(ToGeometry(m), mat);
        if (twoSided) model.BackMaterial = mat;
        return new ModelVisual3D { Content = model };
    }

    public static Visual3D Marker(Vec3 p, Color color, double r = 0.8) =>
        new SphereVisual3D { Center = P(p), Radius = r, Fill = new SolidColorBrush(color), ThetaDiv = 12, PhiDiv = 8 };

    public static Visual3D Label(Vec3 p, string text, Color color) =>
        new BillboardTextVisual3D
        {
            Position = P(p) + new Vector3D(0, 0, 2.5),
            Text = text,
            Foreground = new SolidColorBrush(color),
            Background = Brushes.Transparent,
            FontSize = 11,
        };

    public static Visual3D Curve(IReadOnlyList<Vec3> pts, Color color, double diameter = 0.5) =>
        new TubeVisual3D
        {
            Path = new Point3DCollection(pts.Select(P)),
            Diameter = diameter,
            ThetaDiv = 8,
            Fill = new SolidColorBrush(color),
        };

    public static Visual3D Line(Vec3 a, Vec3 b, Color color, double thickness = 1.5) =>
        new LinesVisual3D { Points = new Point3DCollection { P(a), P(b) }, Color = color, Thickness = thickness };
}
