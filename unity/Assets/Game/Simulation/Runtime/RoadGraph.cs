namespace PampaSkylines.Simulation
{
using System.Collections.Generic;
using System.Linq;
using PampaSkylines.Core;

public sealed class RoadGraph
{
    private readonly Dictionary<string, List<string>> _adjacency = new();

    public RoadGraph(WorldState state)
    {
        foreach (var node in state.RoadNodes)
        {
            _adjacency[node.Id] = new List<string>();
        }

        foreach (var segment in state.RoadSegments)
        {
            if (!_adjacency.TryGetValue(segment.FromNodeId, out var fromList))
            {
                fromList = new List<string>();
                _adjacency[segment.FromNodeId] = fromList;
            }

            if (!_adjacency.TryGetValue(segment.ToNodeId, out var toList))
            {
                toList = new List<string>();
                _adjacency[segment.ToNodeId] = toList;
            }

            fromList.Add(segment.ToNodeId);
            toList.Add(segment.FromNodeId);
        }
    }

    public bool HasRoadAccess(string nodeId)
    {
        return _adjacency.TryGetValue(nodeId, out var neighbors) && neighbors.Count > 0;
    }

    public float AverageCongestion(WorldState state)
    {
        return state.RoadSegments.Count == 0
            ? 0f
            : state.RoadSegments.Average(static segment => segment.Congestion);
    }
}
}
