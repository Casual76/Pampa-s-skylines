#nullable enable

namespace PampaSkylines.PC
{
using System.Collections.Generic;
using System.Linq;
using PampaSkylines.Core;
using PampaSkylines.Shared;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class PcWorldView : MonoBehaviour
{
    private readonly Dictionary<string, GameObject> _roadVisuals = new();
    private readonly Dictionary<string, GameObject> _lotVisuals = new();
    private readonly Dictionary<string, GameObject> _buildingVisuals = new();
    private readonly Dictionary<string, GameObject> _trafficMarkers = new();
    private readonly List<GameObject> _gridLines = new();
    private readonly List<GameObject> _hoverOutlineSegments = new();
    private readonly List<GameObject> _dragOutlineSegments = new();
    private readonly List<GameObject> _serviceCoverageOutlineSegments = new();

    [SerializeField] private PcVisualTheme? visualTheme;

    private PcVisualTheme? _resolvedTheme;
    private MaterialPropertyBlock? _propertyBlock;
    private Transform? _gridRoot;
    private Transform? _roadsRoot;
    private Transform? _lotsRoot;
    private Transform? _buildingsRoot;
    private Transform? _previewRoot;
    private GameObject? _ground;
    private GameObject? _dragFillIndicator;
    private GameObject? _roadPreviewIndicator;
    private GameObject? _serviceCoverageFillIndicator;
    private GameObject? _eventPulseIndicator;
    private float _cellSize = 1f;
    private int _visibleGridHalfExtent = 24;
    private int _gridSignature = int.MinValue;

    private PcVisualTheme Theme => _resolvedTheme ??= visualTheme ?? PcVisualTheme.LoadOrCreateDefault();

    public void BindTheme(PcVisualTheme? theme)
    {
        if (theme is null)
        {
            return;
        }

        visualTheme = theme;
        _resolvedTheme = theme;
    }

    private void Awake()
    {
        _propertyBlock = new MaterialPropertyBlock();
    }

    public void Configure(float cellSize, int visibleGridHalfExtent)
    {
        _cellSize = Mathf.Max(0.5f, cellSize);
        _visibleGridHalfExtent = Mathf.Max(8, visibleGridHalfExtent);
        EnsureRoots();
        EnsureGround();
        EnsureGrid();
        EnsureIndicators();
    }

    public void Render(WorldState state, Int2? hoverCell, Int2? dragStartCell, PcToolMode toolMode, PcOverlayState? overlayState)
    {
        EnsureRoots();
        EnsureGround();
        EnsureGrid();
        EnsureIndicators();
        if (_gridRoot is not null)
        {
            _gridRoot.gameObject.SetActive(overlayState?.ShowGrid ?? true);
        }

        SyncRoads(state, overlayState);
        SyncLots(state, overlayState);
        SyncBuildings(state, overlayState);
        UpdateIndicators(state, hoverCell, dragStartCell, toolMode, overlayState);
    }

    private void EnsureRoots()
    {
        _gridRoot ??= CreateRoot("Grid");
        _roadsRoot ??= CreateRoot("Roads");
        _lotsRoot ??= CreateRoot("Lots");
        _buildingsRoot ??= CreateRoot("Buildings");
        _previewRoot ??= CreateRoot("Preview");
    }

    private Transform CreateRoot(string rootName)
    {
        var root = new GameObject(rootName).transform;
        root.SetParent(transform, false);
        return root;
    }

    private void EnsureGround()
    {
        if (_ground is null)
        {
            _ground = CreateCube("Ground", transform, Theme.GroundMaterial);
        }

        var worldSize = ((_visibleGridHalfExtent * 2) + 5) * _cellSize;
        _ground.transform.position = new Vector3(0f, -0.045f, 0f);
        _ground.transform.localScale = new Vector3(worldSize, 0.08f, worldSize);
        ApplyColor(_ground, Theme.GroundMaterial, Theme.GroundColor);
    }

    private void EnsureGrid()
    {
        if (_gridRoot is null)
        {
            return;
        }

        var signature = (_visibleGridHalfExtent * 1000) + Mathf.RoundToInt(_cellSize * 100f);
        if (signature == _gridSignature)
        {
            return;
        }

        foreach (var line in _gridLines)
        {
            Destroy(line);
        }

        _gridLines.Clear();
        _gridSignature = signature;

        var gridWorldSize = ((_visibleGridHalfExtent * 2) + 1) * _cellSize;
        var boundaryCount = (_visibleGridHalfExtent * 2) + 2;
        var lineThickness = Mathf.Max(0.03f, _cellSize * 0.025f);
        var majorLineThickness = lineThickness * 1.8f;

        for (var index = 0; index < boundaryCount; index++)
        {
            var cellBoundary = index - _visibleGridHalfExtent - 1;
            var coordinate = (cellBoundary + 0.5f) * _cellSize;
            var isMajor = Mathf.Abs(cellBoundary + 1) % 4 == 0;
            var color = isMajor ? Theme.GridMajorColor : Theme.GridMinorColor;
            var thickness = isMajor ? majorLineThickness : lineThickness;

            var verticalLine = CreateCube($"GridX_{index}", _gridRoot, Theme.GridLineMaterial);
            verticalLine.transform.position = new Vector3(coordinate, 0.001f, 0f);
            verticalLine.transform.localScale = new Vector3(thickness, 0.01f, gridWorldSize);
            ApplyColor(verticalLine, Theme.GridLineMaterial, color);
            _gridLines.Add(verticalLine);

            var horizontalLine = CreateCube($"GridZ_{index}", _gridRoot, Theme.GridLineMaterial);
            horizontalLine.transform.position = new Vector3(0f, 0.001f, coordinate);
            horizontalLine.transform.localScale = new Vector3(gridWorldSize, 0.01f, thickness);
            ApplyColor(horizontalLine, Theme.GridLineMaterial, color);
            _gridLines.Add(horizontalLine);
        }
    }

    private void EnsureIndicators()
    {
        if (_previewRoot is null)
        {
            return;
        }

        EnsureOutlineSegments(_hoverOutlineSegments, "Hover", _previewRoot, Theme.HoverMaterial);
        EnsureOutlineSegments(_dragOutlineSegments, "Drag", _previewRoot, Theme.DragPreviewMaterial);
        EnsureOutlineSegments(_serviceCoverageOutlineSegments, "Coverage", _previewRoot, Theme.DragPreviewMaterial);

        _dragFillIndicator ??= CreateCube("DragFill", _previewRoot, Theme.DragPreviewMaterial);
        _roadPreviewIndicator ??= CreateCube("RoadPreview", _previewRoot, Theme.DragPreviewMaterial);
        _serviceCoverageFillIndicator ??= CreateCube("ServiceCoverageFill", _previewRoot, Theme.DragPreviewMaterial);
        _eventPulseIndicator ??= CreateCube("EventPulse", _previewRoot, Theme.DragPreviewMaterial);
    }

    private void SyncRoads(WorldState state, PcOverlayState? overlayState)
    {
        if (_roadsRoot is null)
        {
            return;
        }

        var nodeLookup = state.RoadNodes.ToDictionary(node => node.Id, node => node);
        var activeIds = new HashSet<string>();

        foreach (var segment in state.RoadSegments)
        {
            if (!nodeLookup.TryGetValue(segment.FromNodeId, out var fromNode) || !nodeLookup.TryGetValue(segment.ToNodeId, out var toNode))
            {
                continue;
            }

            activeIds.Add(segment.Id);
            if (!_roadVisuals.TryGetValue(segment.Id, out var visual))
            {
                visual = CreateCube($"Road_{segment.Id}", _roadsRoot, Theme.RoadMaterial);
                _roadVisuals[segment.Id] = visual;
            }

            var start = ToWorld(fromNode.Position, 0.035f);
            var end = ToWorld(toNode.Position, 0.035f);
            var center = (start + end) * 0.5f;
            var delta = end - start;
            var width = Mathf.Max(_cellSize * 0.30f, segment.Lanes * 0.11f * _cellSize);
            visual.transform.SetPositionAndRotation(center, Quaternion.LookRotation(delta.normalized, Vector3.up));
            visual.transform.localScale = new Vector3(width, 0.06f, Mathf.Max(_cellSize * 0.25f, delta.magnitude + (_cellSize * 0.10f)));
            var congestion = Mathf.Clamp01(segment.Congestion);
            if (congestion >= 0.60f)
            {
                congestion = Mathf.Clamp01(0.60f + ((congestion - 0.60f) * 1.85f));
            }

            var roadColor = overlayState?.ActiveOverlay == PcOverlayKind.Traffic
                ? Theme.GetRoadColor(Mathf.Clamp01(congestion * 1.35f))
                : Theme.GetRoadColor(congestion);
            ApplyColor(visual, Theme.RoadMaterial, roadColor);
            SyncTrafficMarker(segment, start, end, overlayState);
        }

        RemoveMissing(_roadVisuals, activeIds);
        RemoveMissing(_trafficMarkers, activeIds);
    }

    private void SyncLots(WorldState state, PcOverlayState? overlayState)
    {
        if (_lotsRoot is null)
        {
            return;
        }

        var activeIds = new HashSet<string>();
        foreach (var lot in state.Lots)
        {
            activeIds.Add(lot.Id);
            if (!_lotVisuals.TryGetValue(lot.Id, out var visual))
            {
                visual = CreateCube($"Lot_{lot.Id}", _lotsRoot, Theme.ZoneMaterial);
                _lotVisuals[lot.Id] = visual;
            }

            visual.transform.position = ToWorld(lot.Cell, 0.015f);
            visual.transform.localScale = new Vector3(_cellSize * 0.88f, 0.02f, _cellSize * 0.88f);
            ApplyColor(visual, Theme.ZoneMaterial, ResolveLotColor(lot, overlayState));
        }

        RemoveMissing(_lotVisuals, activeIds);
    }

    private void SyncBuildings(WorldState state, PcOverlayState? overlayState)
    {
        if (_buildingsRoot is null)
        {
            return;
        }

        var activeIds = new HashSet<string>();
        foreach (var building in state.Buildings)
        {
            activeIds.Add(building.Id);
            if (!_buildingVisuals.TryGetValue(building.Id, out var visual))
            {
                visual = CreateCube($"Building_{building.Id}", _buildingsRoot, Theme.BuildingMaterial);
                visual.GetComponent<Renderer>()!.shadowCastingMode = ShadowCastingMode.On;
                visual.GetComponent<Renderer>()!.receiveShadows = true;
                CreateBuildingDecoration(visual);
                _buildingVisuals[building.Id] = visual;
            }

            var variation = Hash01(building.Id);
            var variationBucket = Mathf.Clamp(Mathf.FloorToInt(variation * 3f), 0, 2);
            var height = Mathf.Max(0.45f, 0.62f + (building.Level * 0.34f) + (variationBucket * 0.24f));
            if (building.ServiceType != ServiceType.None)
            {
                height += 0.55f;
            }

            var footprint = building.ServiceType != ServiceType.None
                ? 0.80f
                : variationBucket switch
                {
                    0 => 0.58f,
                    1 => 0.70f,
                    _ => 0.76f
                };
            var depthFootprint = building.ServiceType != ServiceType.None
                ? 0.80f
                : variationBucket switch
                {
                    0 => 0.78f,
                    1 => 0.66f,
                    _ => 0.60f
                };
            visual.transform.position = ToWorld(building.Cell, (height * 0.5f) + 0.03f);
            visual.transform.rotation = Quaternion.Euler(0f, variation > 0.55f ? 90f : 0f, 0f);
            visual.transform.localScale = new Vector3(_cellSize * footprint, height, _cellSize * depthFootprint);
            ApplyColor(visual, Theme.BuildingMaterial, ResolveBuildingColor(building, overlayState));
            UpdateBuildingDecoration(visual, building, variationBucket, height);
        }

        RemoveMissing(_buildingVisuals, activeIds);
    }

    private void UpdateIndicators(WorldState state, Int2? hoverCell, Int2? dragStartCell, PcToolMode toolMode, PcOverlayState? overlayState)
    {
        SetActive(_hoverOutlineSegments, false);
        SetActive(_dragOutlineSegments, false);
        SetActive(_serviceCoverageOutlineSegments, false);

        if (_dragFillIndicator is not null)
        {
            _dragFillIndicator.SetActive(false);
        }

        if (_roadPreviewIndicator is not null)
        {
            _roadPreviewIndicator.SetActive(false);
        }

        if (_serviceCoverageFillIndicator is not null)
        {
            _serviceCoverageFillIndicator.SetActive(false);
        }

        if (hoverCell.HasValue)
        {
            UpdateOutline(
                _hoverOutlineSegments,
                hoverCell.Value,
                hoverCell.Value,
                0.055f,
                Mathf.Max(0.04f, _cellSize * 0.065f),
                Theme.HoverMaterial,
                Theme.GetHoverColor(toolMode));
        }

        UpdateServiceCoverageIndicator(state, hoverCell, toolMode, overlayState);
        UpdateEventPulseIndicator(state);

        var showDrag = hoverCell.HasValue && dragStartCell.HasValue && toolMode.IsDragTool();
        if (!showDrag)
        {
            return;
        }

        if (toolMode == PcToolMode.Road && _roadPreviewIndicator is not null)
        {
            var startWorld = ToWorld(dragStartCell!.Value, 0.028f);
            var endWorld = ToWorld(hoverCell!.Value, 0.028f);
            var delta = endWorld - startWorld;
            var rotation = delta.sqrMagnitude < 0.0001f
                ? Quaternion.identity
                : Quaternion.LookRotation(delta.normalized, Vector3.up);
            _roadPreviewIndicator.SetActive(true);
            _roadPreviewIndicator.transform.SetPositionAndRotation((startWorld + endWorld) * 0.5f, rotation);
            _roadPreviewIndicator.transform.localScale = new Vector3(_cellSize * 0.34f, 0.03f, Mathf.Max(_cellSize * 0.35f, delta.magnitude + (_cellSize * 0.12f)));
            ApplyColor(_roadPreviewIndicator, Theme.DragPreviewMaterial, Theme.GetDragFillColor(toolMode));
            return;
        }

        if (_dragFillIndicator is null)
        {
            return;
        }

        var startCell = dragStartCell.Value;
        var currentHoverCell = hoverCell.Value;
        var minX = Mathf.Min(startCell.X, currentHoverCell.X);
        var maxX = Mathf.Max(startCell.X, currentHoverCell.X);
        var minY = Mathf.Min(startCell.Y, currentHoverCell.Y);
        var maxY = Mathf.Max(startCell.Y, currentHoverCell.Y);
        var width = ((maxX - minX) + 1) * _cellSize;
        var depth = ((maxY - minY) + 1) * _cellSize;
        var center = new Vector3((minX + maxX) * 0.5f * _cellSize, 0.012f, (minY + maxY) * 0.5f * _cellSize);

        _dragFillIndicator.SetActive(true);
        _dragFillIndicator.transform.SetPositionAndRotation(center, Quaternion.identity);
        _dragFillIndicator.transform.localScale = new Vector3(
            Mathf.Max(_cellSize * 0.25f, width - (_cellSize * 0.06f)),
            0.012f,
            Mathf.Max(_cellSize * 0.25f, depth - (_cellSize * 0.06f)));
        ApplyColor(_dragFillIndicator, Theme.DragPreviewMaterial, Theme.GetDragFillColor(toolMode));
        UpdateOutline(
            _dragOutlineSegments,
            new Int2(minX, minY),
            new Int2(maxX, maxY),
            0.020f,
            Mathf.Max(0.04f, _cellSize * 0.075f),
            Theme.DragPreviewMaterial,
            Theme.GetDragOutlineColor(toolMode));
    }

    private void UpdateServiceCoverageIndicator(WorldState state, Int2? hoverCell, PcToolMode toolMode, PcOverlayState? overlayState)
    {
        if (!hoverCell.HasValue)
        {
            return;
        }

        var showCoverage = overlayState?.ActiveOverlay == PcOverlayKind.ServiceCoverage || toolMode.IsServiceTool();
        if (!showCoverage || _serviceCoverageFillIndicator is null)
        {
            return;
        }

        var serviceColor = toolMode.IsServiceTool()
            ? Theme.GetServiceColor(toolMode.ToServiceType())
            : Theme.HudAccentColor;

        var coverageRadius = ResolveCoverageRadius(state, hoverCell.Value, toolMode);
        var width = ((coverageRadius * 2f) + 1f) * _cellSize;
        var center = new Vector3(hoverCell.Value.X * _cellSize, 0.010f, hoverCell.Value.Y * _cellSize);

        _serviceCoverageFillIndicator.SetActive(true);
        _serviceCoverageFillIndicator.transform.SetPositionAndRotation(center, Quaternion.identity);
        _serviceCoverageFillIndicator.transform.localScale = new Vector3(width, 0.008f, width);
        var pulse = 0.18f + (Mathf.Sin(Time.realtimeSinceStartup * 2.4f) * 0.08f);
        ApplyColor(_serviceCoverageFillIndicator, Theme.DragPreviewMaterial, new Color(serviceColor.r, serviceColor.g, serviceColor.b, pulse));
        UpdateOutline(
            _serviceCoverageOutlineSegments,
            new Int2(hoverCell.Value.X - Mathf.RoundToInt(coverageRadius), hoverCell.Value.Y - Mathf.RoundToInt(coverageRadius)),
            new Int2(hoverCell.Value.X + Mathf.RoundToInt(coverageRadius), hoverCell.Value.Y + Mathf.RoundToInt(coverageRadius)),
            0.018f,
            Mathf.Max(0.03f, _cellSize * 0.05f),
            Theme.DragPreviewMaterial,
            new Color(serviceColor.r, serviceColor.g, serviceColor.b, 0.92f));
    }

    private void UpdateEventPulseIndicator(WorldState state)
    {
        if (_eventPulseIndicator is null)
        {
            return;
        }

        var activeEvent = state.RunState.ActiveEvent;
        if (activeEvent is null)
        {
            _eventPulseIndicator.SetActive(false);
            return;
        }

        var center = ResolveCityCenter(state);
        var pulse = 0.35f + (Mathf.Sin(Time.realtimeSinceStartup * 3.2f) * 0.15f);
        var radius = Mathf.Lerp(_cellSize * 1.2f, _cellSize * 2.3f, Mathf.Abs(Mathf.Sin(Time.realtimeSinceStartup * 2f)));

        _eventPulseIndicator.SetActive(true);
        _eventPulseIndicator.transform.SetPositionAndRotation(center + new Vector3(0f, 0.018f, 0f), Quaternion.identity);
        _eventPulseIndicator.transform.localScale = new Vector3(radius, 0.014f, radius);
        var color = Color.Lerp(Theme.HudWarningColor, Theme.HudErrorColor, 0.28f);
        ApplyColor(_eventPulseIndicator, Theme.DragPreviewMaterial, new Color(color.r, color.g, color.b, pulse));
    }

    private void EnsureOutlineSegments(List<GameObject> segments, string prefix, Transform parent, Material? material)
    {
        while (segments.Count < 4)
        {
            var segment = CreateCube($"{prefix}Outline_{segments.Count}", parent, material);
            segment.SetActive(false);
            segments.Add(segment);
        }
    }

    private void UpdateOutline(
        List<GameObject> segments,
        Int2 minCell,
        Int2 maxCell,
        float y,
        float thickness,
        Material? material,
        Color color)
    {
        if (segments.Count < 4)
        {
            return;
        }

        SetActive(segments, true);

        var width = ((maxCell.X - minCell.X) + 1) * _cellSize;
        var depth = ((maxCell.Y - minCell.Y) + 1) * _cellSize;
        var centerX = (minCell.X + maxCell.X) * 0.5f * _cellSize;
        var centerZ = (minCell.Y + maxCell.Y) * 0.5f * _cellSize;

        ConfigureOutlineSegment(segments[0], new Vector3(centerX, y, centerZ + (depth * 0.5f) - (thickness * 0.5f)), new Vector3(width, thickness, thickness), material, color);
        ConfigureOutlineSegment(segments[1], new Vector3(centerX, y, centerZ - (depth * 0.5f) + (thickness * 0.5f)), new Vector3(width, thickness, thickness), material, color);
        ConfigureOutlineSegment(segments[2], new Vector3(centerX - (width * 0.5f) + (thickness * 0.5f), y, centerZ), new Vector3(thickness, thickness, depth), material, color);
        ConfigureOutlineSegment(segments[3], new Vector3(centerX + (width * 0.5f) - (thickness * 0.5f), y, centerZ), new Vector3(thickness, thickness, depth), material, color);
    }

    private void ConfigureOutlineSegment(GameObject segment, Vector3 position, Vector3 scale, Material? material, Color color)
    {
        segment.transform.SetPositionAndRotation(position, Quaternion.identity);
        segment.transform.localScale = scale;
        ApplyColor(segment, material, color);
    }

    private GameObject CreateCube(string objectName, Transform parent, Material? material)
    {
        var cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = objectName;
        cube.transform.SetParent(parent, false);
        RemoveCollider(cube);
        var renderer = cube.GetComponent<Renderer>();
        if (renderer is not null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            if (material is not null)
            {
                renderer.sharedMaterial = material;
            }
        }

        return cube;
    }

    private void SyncTrafficMarker(RoadSegment segment, Vector3 start, Vector3 end, PcOverlayState? overlayState)
    {
        var shouldShow = overlayState?.ActiveOverlay == PcOverlayKind.Traffic || segment.Congestion >= 0.35f;
        if (!shouldShow)
        {
            if (_trafficMarkers.TryGetValue(segment.Id, out var existing))
            {
                existing.SetActive(false);
            }

            return;
        }

        if (!_trafficMarkers.TryGetValue(segment.Id, out var marker))
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = $"Traffic_{segment.Id}";
            marker.transform.SetParent(_roadsRoot, false);
            RemoveCollider(marker);
            var renderer = marker.GetComponent<Renderer>();
            if (renderer is not null)
            {
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                renderer.receiveShadows = false;
            }

            _trafficMarkers[segment.Id] = marker;
        }

        marker.SetActive(true);
        var progress = Mathf.Repeat((Time.realtimeSinceStartup * (0.45f + segment.Congestion)) + Hash01(segment.Id), 1f);
        var position = Vector3.Lerp(start, end, progress);
        marker.transform.SetPositionAndRotation(position + new Vector3(0f, 0.09f, 0f), Quaternion.identity);
        marker.transform.localScale = Vector3.one * Mathf.Max(0.08f, _cellSize * 0.09f);
        ApplyColor(marker, Theme.DragPreviewMaterial, Theme.GetRoadColor(Mathf.Clamp01(segment.Congestion * 1.2f)));
    }

    private void CreateBuildingDecoration(GameObject buildingVisual)
    {
        var roof = CreateCube("RoofCap", buildingVisual.transform, Theme.BuildingMaterial);
        roof.transform.localPosition = new Vector3(0f, 0.55f, 0f);
        roof.transform.localScale = new Vector3(0.82f, 0.16f, 0.82f);
        ApplyColor(roof, Theme.BuildingMaterial, new Color(0.86f, 0.88f, 0.90f, 1f));
    }

    private void UpdateBuildingDecoration(GameObject buildingVisual, BuildingState building, int variationBucket, float height)
    {
        var roof = buildingVisual.transform.Find("RoofCap");
        if (roof is null)
        {
            return;
        }

        var width = variationBucket switch
        {
            0 => 0.74f,
            1 => 0.80f,
            _ => 0.86f
        };
        roof.localPosition = new Vector3(0f, Mathf.Max(0.16f, (height * 0.5f) + 0.03f), 0f);
        roof.localScale = new Vector3(width, Mathf.Clamp(0.12f + (building.Level * 0.03f), 0.12f, 0.24f), width);
        var roofColor = building.ServiceType != ServiceType.None
            ? Color.Lerp(Theme.GetBuildingColor(building), Color.white, 0.35f)
            : Color.Lerp(Theme.GetBuildingColor(building), Color.black, 0.12f);
        ApplyColor(roof.gameObject, Theme.BuildingMaterial, roofColor);
    }

    private void RemoveCollider(GameObject gameObject)
    {
        var collider = gameObject.GetComponent<Collider>();
        if (collider is not null)
        {
            Destroy(collider);
        }
    }

    private void ApplyColor(GameObject gameObject, Material? material, Color color)
    {
        var renderer = gameObject.GetComponent<Renderer>();
        if (renderer is null)
        {
            return;
        }

        if (material is not null && renderer.sharedMaterial != material)
        {
            renderer.sharedMaterial = material;
        }

        var propertyBlock = _propertyBlock ??= new MaterialPropertyBlock();
        propertyBlock.Clear();
        var activeMaterial = renderer.sharedMaterial;
        if (activeMaterial is not null)
        {
            if (activeMaterial.HasProperty("_BaseColor"))
            {
                propertyBlock.SetColor("_BaseColor", color);
            }

            if (activeMaterial.HasProperty("_Color"))
            {
                propertyBlock.SetColor("_Color", color);
            }
        }

        renderer.SetPropertyBlock(propertyBlock);
    }

    private Color ResolveLotColor(ZoneLot lot, PcOverlayState? overlayState)
    {
        var readiness = ComputeUtilityReadiness(lot);
        var hasProblem = readiness < 0.80f || !lot.HasRoadAccess;
        var healthyTint = new Color(0.24f, 0.78f, 0.42f, 0.42f);
        var problemTint = new Color(0.82f, 0.26f, 0.20f, 0.52f);
        return overlayState?.ActiveOverlay switch
        {
            PcOverlayKind.LandValue => Color.Lerp(new Color(0.82f, 0.18f, 0.14f, 0.60f), new Color(0.17f, 0.82f, 0.38f, 0.62f), Mathf.Clamp01(lot.LandValue)),
            PcOverlayKind.Utilities => Color.Lerp(new Color(0.88f, 0.24f, 0.16f, 0.62f), new Color(0.14f, 0.74f, 0.86f, 0.62f), readiness),
            _ => hasProblem
                ? Color.Lerp(Theme.GetLotColor(lot), problemTint, Mathf.Clamp01(1f - readiness))
                : Color.Lerp(Theme.GetLotColor(lot), healthyTint, 0.22f)
        };
    }

    private Color ResolveBuildingColor(BuildingState building, PcOverlayState? overlayState)
    {
        return overlayState?.ActiveOverlay switch
        {
            PcOverlayKind.ServiceCoverage when building.ServiceType != ServiceType.None => Theme.GetServiceColor(building.ServiceType),
            PcOverlayKind.Traffic when building.ServiceType == ServiceType.None => Color.Lerp(Theme.GetBuildingColor(building), Theme.RoadCongestedColor, 0.18f),
            _ => Theme.GetBuildingColor(building)
        };
    }

    private float ResolveCoverageRadius(WorldState state, Int2 hoverCell, PcToolMode toolMode)
    {
        var building = state.Buildings.FirstOrDefault(existing => existing.Cell.Equals(hoverCell) && existing.ServiceType != ServiceType.None);
        if (building is not null)
        {
            return Mathf.Max(1f, building.CoverageRadius);
        }

        return toolMode.ToServiceType() switch
        {
            ServiceType.Fire => 8f,
            ServiceType.Police => 8f,
            ServiceType.Health => 8f,
            ServiceType.Education => 8f,
            ServiceType.Electricity => 6f,
            ServiceType.Water => 6f,
            ServiceType.Sewage => 6f,
            ServiceType.Waste => 6f,
            _ => 4f
        };
    }

    private static float ComputeUtilityReadiness(ZoneLot lot)
    {
        var readiness = 0f;
        readiness += lot.HasRoadAccess ? 1f : 0f;
        readiness += lot.HasElectricity ? 1f : 0f;
        readiness += lot.HasWater ? 1f : 0f;
        readiness += lot.HasSewage ? 1f : 0f;
        readiness += lot.HasWaste ? 1f : 0f;
        return readiness / 5f;
    }

    private static void SetActive(List<GameObject> objects, bool isActive)
    {
        foreach (var instance in objects)
        {
            instance.SetActive(isActive);
        }
    }

    private Vector3 ToWorld(Int2 cell, float y)
    {
        return new Vector3(cell.X * _cellSize, y, cell.Y * _cellSize);
    }

    private Vector3 ResolveCityCenter(WorldState state)
    {
        if (state.Buildings.Count > 0)
        {
            var x = state.Buildings.Average(building => building.Cell.X);
            var z = state.Buildings.Average(building => building.Cell.Y);
            return new Vector3((float)x * _cellSize, 0f, (float)z * _cellSize);
        }

        if (state.Lots.Count > 0)
        {
            var x = state.Lots.Average(lot => lot.Cell.X);
            var z = state.Lots.Average(lot => lot.Cell.Y);
            return new Vector3((float)x * _cellSize, 0f, (float)z * _cellSize);
        }

        return Vector3.zero;
    }

    private static float Hash01(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return 0.5f;
        }

        unchecked
        {
            var hash = 17;
            foreach (var character in value)
            {
                hash = (hash * 31) + character;
            }

            return Mathf.Abs(hash % 1000) / 999f;
        }
    }

    private void RemoveMissing(Dictionary<string, GameObject> visuals, HashSet<string> activeIds)
    {
        var staleKeys = visuals.Keys.Where(key => !activeIds.Contains(key)).ToList();
        foreach (var staleKey in staleKeys)
        {
            Destroy(visuals[staleKey]);
            visuals.Remove(staleKey);
        }
    }
}
}
