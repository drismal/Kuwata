using System.Windows.Media;
using Kuwata.Core.Project;

namespace Kuwata.App;

public sealed class LandmarkItem
{
    public required Landmark Landmark { get; init; }
    public required string Title { get; init; }
    public required string CoordText { get; init; }
    public required Brush Brush { get; init; }
}
