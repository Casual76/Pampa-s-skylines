#nullable enable

namespace PampaSkylines.Commands
{
using System;

public sealed class GameCommand
{
    public int SchemaVersion { get; set; } = 1;

    public string CommandId { get; set; } = Guid.NewGuid().ToString("N");

    public string ClientId { get; set; } = "local";

    public long ClientSequence { get; set; }

    public DateTimeOffset IssuedAtUtc { get; set; } = DateTimeOffset.UtcNow;

    public GameCommandType Type { get; set; }

    public BuildRoadCommandData? BuildRoad { get; set; }

    public ZonePaintCommandData? PaintZone { get; set; }

    public PlaceServiceCommandData? PlaceService { get; set; }

    public BulldozeCommandData? Bulldoze { get; set; }

    public BudgetPolicyCommandData? BudgetPolicy { get; set; }

    public TimeControlCommandData? TimeControl { get; set; }

    public ResolveEventChoiceCommandData? ResolveEventChoice { get; set; }
}
}
