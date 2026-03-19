using System.Drawing;

namespace HaloLight.Models;

public sealed class DisplayInfo
{
    public required string DeviceName { get; init; }
    public required string DisplayName { get; init; }
    public required Rectangle Bounds { get; init; }
    public required bool IsPrimary { get; init; }
}
