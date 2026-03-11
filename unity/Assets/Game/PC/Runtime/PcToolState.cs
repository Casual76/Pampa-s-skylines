#nullable enable

namespace PampaSkylines.PC
{
using PampaSkylines.Shared;

public sealed class PcToolState
{
    public PcToolMode ActiveToolMode { get; private set; } = PcToolMode.Road;

    public Int2? HoverCell { get; private set; }

    public Int2? DragStartCell { get; private set; }

    public string SelectedRoadTypeId { get; private set; } = "road-2lane";

    public int SelectedRoadLanes { get; private set; } = 2;

    public void SelectTool(PcToolMode toolMode)
    {
        ActiveToolMode = toolMode;
    }

    public void SetHoverCell(Int2? hoverCell)
    {
        HoverCell = hoverCell;
    }

    public void BeginDrag(Int2 cell)
    {
        DragStartCell = cell;
    }

    public void ClearDrag()
    {
        DragStartCell = null;
    }

    public void SetRoadPreset(string roadTypeId, int lanes)
    {
        if (!string.IsNullOrWhiteSpace(roadTypeId))
        {
            SelectedRoadTypeId = roadTypeId;
        }

        SelectedRoadLanes = lanes < 1 ? 1 : lanes;
    }
}
}
