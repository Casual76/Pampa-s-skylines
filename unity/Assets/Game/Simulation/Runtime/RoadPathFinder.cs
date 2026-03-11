#nullable enable

namespace PampaSkylines.Simulation
{
using System;
using System.Collections.Generic;
using System.Linq;
using PampaSkylines.Core;
using PampaSkylines.Shared;

public static class RoadPathFinder
{
    public static IReadOnlyList<RoadSegment> FindPathSegments(WorldState state, Int2 startPosition, Int2 endPosition)
    {
        if (state.RoadNodes.Count == 0 || state.RoadSegments.Count == 0)
        {
            return Array.Empty<RoadSegment>();
        }

        var startNode = FindClosestNode(state, startPosition);
        var endNode = FindClosestNode(state, endPosition);
        if (startNode is null || endNode is null)
        {
            return Array.Empty<RoadSegment>();
        }

        if (startNode.Id == endNode.Id)
        {
            return Array.Empty<RoadSegment>();
        }

        var adjacency = BuildAdjacency(state.RoadSegments);
        var distances = state.RoadNodes.ToDictionary(node => node.Id, _ => double.PositiveInfinity);
        var previous = new Dictionary<string, RoadSegment>();
        var pending = new HashSet<string>(distances.Keys);

        distances[startNode.Id] = 0d;

        while (pending.Count > 0)
        {
            var currentNodeId = pending
                .OrderBy(nodeId => distances[nodeId])
                .ThenBy(static nodeId => nodeId, System.StringComparer.Ordinal)
                .First();

            pending.Remove(currentNodeId);

            if (double.IsPositiveInfinity(distances[currentNodeId]) || currentNodeId == endNode.Id)
            {
                break;
            }

            if (!adjacency.TryGetValue(currentNodeId, out var connectedSegments))
            {
                continue;
            }

            foreach (var segment in connectedSegments)
            {
                var neighborId = segment.FromNodeId == currentNodeId ? segment.ToNodeId : segment.FromNodeId;
                if (!pending.Contains(neighborId))
                {
                    continue;
                }

                var travelCost = segment.Length * (1d + Math.Max(0f, segment.Congestion));
                var alternateDistance = distances[currentNodeId] + travelCost;
                if (alternateDistance < distances[neighborId])
                {
                    distances[neighborId] = alternateDistance;
                    previous[neighborId] = segment;
                }
            }
        }

        if (!previous.ContainsKey(endNode.Id))
        {
            return Array.Empty<RoadSegment>();
        }

        var path = new List<RoadSegment>();
        var nodeId = endNode.Id;
        while (previous.TryGetValue(nodeId, out var segment))
        {
            path.Add(segment);
            nodeId = segment.FromNodeId == nodeId ? segment.ToNodeId : segment.FromNodeId;
        }

        path.Reverse();
        return path;
    }

    private static RoadNode? FindClosestNode(WorldState state, Int2 position)
    {
        return state.RoadNodes
            .OrderBy(node => node.Position.ManhattanDistance(position))
            .FirstOrDefault();
    }

    private static Dictionary<string, List<RoadSegment>> BuildAdjacency(IReadOnlyList<RoadSegment> segments)
    {
        var adjacency = new Dictionary<string, List<RoadSegment>>();

        foreach (var segment in segments)
        {
            if (!adjacency.TryGetValue(segment.FromNodeId, out var from))
            {
                from = new List<RoadSegment>();
                adjacency[segment.FromNodeId] = from;
            }

            if (!adjacency.TryGetValue(segment.ToNodeId, out var to))
            {
                to = new List<RoadSegment>();
                adjacency[segment.ToNodeId] = to;
            }

            from.Add(segment);
            to.Add(segment);
        }

        return adjacency;
    }
}
}
