using System.IO.Compression;
using System.Text.Json;
using System.Text.Json.Serialization;
using Kuwata.Core.IO;

namespace Kuwata.Core.Project;

/// <summary>
/// Файл проекта (*.kwp) — zip: project.json + meshes/&lt;роль&gt;.stl.
/// Модели хранятся в исходных координатах; преобразование пересчитывается при открытии.
/// </summary>
public static class ProjectFile
{
    public const string Extension = ".kwp";

    public static readonly JsonSerializerOptions Json = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() },
    };

    public static void Save(KuwataProject project, string path)
    {
        var tmp = path + ".tmp";
        using (var fs = File.Create(tmp))
        using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
        {
            var entry = zip.CreateEntry("project.json");
            using (var s = entry.Open())
                JsonSerializer.Serialize(s, project, Json);
            foreach (var (role, mesh) in project.Meshes)
            {
                var e = zip.CreateEntry($"meshes/{role}.stl", CompressionLevel.Optimal);
                using var s = e.Open();
                StlIO.WriteBinary(s, mesh, role.ToString());
            }
        }
        File.Move(tmp, path, overwrite: true);
    }

    public static KuwataProject Load(string path)
    {
        using var zip = ZipFile.OpenRead(path);
        var pe = zip.GetEntry("project.json") ?? throw new InvalidDataException("В файле нет project.json.");
        KuwataProject project;
        using (var s = pe.Open())
            project = JsonSerializer.Deserialize<KuwataProject>(s, Json) ?? throw new InvalidDataException("Пустой проект.");
        if (project.FormatVersion > 1) throw new InvalidDataException($"Версия формата {project.FormatVersion} новее программы.");
        foreach (var role in Enum.GetValues<MeshRole>())
        {
            var e = zip.GetEntry($"meshes/{role}.stl");
            if (e is null) continue;
            using var s = e.Open();
            project.Meshes[role] = StlIO.Read(s);
        }
        return project;
    }
}
