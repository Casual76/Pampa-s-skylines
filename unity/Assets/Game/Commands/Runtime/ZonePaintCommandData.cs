namespace PampaSkylines.Commands
{
using System.Collections.Generic;
using PampaSkylines.Core;
using PampaSkylines.Shared;

public sealed class ZonePaintCommandData
{
    public ZoneType ZoneType { get; set; }

    public List<Int2> Cells { get; set; } = new();
}
}
