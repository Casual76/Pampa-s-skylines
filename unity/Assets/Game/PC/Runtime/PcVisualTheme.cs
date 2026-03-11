#nullable enable

namespace PampaSkylines.PC
{
using PampaSkylines.Core;
using UnityEngine;

[CreateAssetMenu(menuName = "Pampa Skylines/PC Visual Theme", fileName = "PcCleanSimTheme")]
public sealed class PcVisualTheme : ScriptableObject
{
    public const string DefaultResourcePath = "PcCleanSimTheme";

    [Header("World Materials")]
    public Material? GroundMaterial;
    public Material? GridLineMaterial;
    public Material? RoadMaterial;
    public Material? ZoneMaterial;
    public Material? BuildingMaterial;
    public Material? HoverMaterial;
    public Material? DragPreviewMaterial;

    [Header("World Colors")]
    public Color CameraBackgroundColor = new(0.66f, 0.73f, 0.77f);
    public Color GroundColor = new(0.44f, 0.50f, 0.40f);
    public Color GridMinorColor = new(0.46f, 0.52f, 0.47f, 0.28f);
    public Color GridMajorColor = new(0.30f, 0.37f, 0.33f, 0.44f);
    public Color RoadColor = new(0.16f, 0.17f, 0.19f);
    public Color RoadCongestedColor = new(0.86f, 0.24f, 0.18f);
    public Color ResidentialColor = new(0.26f, 0.62f, 0.33f, 0.36f);
    public Color CommercialColor = new(0.20f, 0.53f, 0.73f, 0.36f);
    public Color IndustrialColor = new(0.68f, 0.50f, 0.17f, 0.36f);
    public Color OfficeColor = new(0.31f, 0.44f, 0.68f, 0.36f);
    public Color ElectricityColor = new(0.90f, 0.69f, 0.18f);
    public Color WaterColor = new(0.18f, 0.57f, 0.82f);
    public Color SewageColor = new(0.41f, 0.51f, 0.24f);
    public Color WasteColor = new(0.51f, 0.44f, 0.18f);
    public Color FireColor = new(0.79f, 0.26f, 0.21f);
    public Color PoliceColor = new(0.19f, 0.30f, 0.65f);
    public Color HealthColor = new(0.16f, 0.61f, 0.47f);
    public Color EducationColor = new(0.68f, 0.40f, 0.21f);
    public Color HoverOutlineColor = new(0.98f, 0.98f, 0.98f, 0.95f);
    public Color DragFillColor = new(0.17f, 0.60f, 0.84f, 0.20f);
    public Color DragOutlineColor = new(0.15f, 0.56f, 0.79f, 0.85f);

    [Header("HUD Colors")]
    public Color HudPanelColor = new(0.08f, 0.10f, 0.13f, 0.90f);
    public Color HudPanelSecondaryColor = new(0.12f, 0.15f, 0.19f, 0.93f);
    public Color HudCardColor = new(0.15f, 0.18f, 0.22f, 0.97f);
    public Color HudAccentColor = new(0.20f, 0.72f, 0.74f, 1f);
    public Color HudTextColor = new(0.92f, 0.95f, 0.97f, 1f);
    public Color HudMutedTextColor = new(0.66f, 0.73f, 0.79f, 1f);
    public Color HudButtonColor = new(0.20f, 0.25f, 0.31f, 1f);
    public Color HudButtonActiveColor = new(0.25f, 0.61f, 0.75f, 1f);
    public Color HudButtonTextColor = new(0.96f, 0.97f, 0.98f, 1f);
    public Color HudWarningColor = new(0.93f, 0.63f, 0.22f, 1f);
    public Color HudErrorColor = new(0.90f, 0.36f, 0.32f, 1f);

    public static PcVisualTheme? LoadDefault()
    {
        return Resources.Load<PcVisualTheme>(DefaultResourcePath);
    }

    public static PcVisualTheme LoadOrCreateDefault()
    {
        return LoadDefault() ?? CreateTransientFallback();
    }

    public static PcVisualTheme CreateTransientFallback()
    {
        var theme = CreateInstance<PcVisualTheme>();
        theme.hideFlags = HideFlags.HideAndDontSave;
        return theme;
    }

    public Color GetToolColor(PcToolMode toolMode)
    {
        return toolMode switch
        {
            PcToolMode.Road => RoadColor,
            PcToolMode.Residential => ResidentialColor,
            PcToolMode.Commercial => CommercialColor,
            PcToolMode.Industrial => IndustrialColor,
            PcToolMode.Office => OfficeColor,
            PcToolMode.Electricity => ElectricityColor,
            PcToolMode.Water => WaterColor,
            PcToolMode.Sewage => SewageColor,
            PcToolMode.Waste => WasteColor,
            PcToolMode.Fire => FireColor,
            PcToolMode.Police => PoliceColor,
            PcToolMode.Health => HealthColor,
            PcToolMode.Education => EducationColor,
            PcToolMode.Bulldoze => HudErrorColor,
            _ => HudAccentColor
        };
    }

    public Color GetZoneColor(ZoneType zoneType)
    {
        return zoneType switch
        {
            ZoneType.Residential => ResidentialColor,
            ZoneType.Commercial => CommercialColor,
            ZoneType.Industrial => IndustrialColor,
            ZoneType.Office => OfficeColor,
            _ => new Color(0.48f, 0.52f, 0.56f, 0.30f)
        };
    }

    public Color GetRoadColor(float congestion)
    {
        return Color.Lerp(RoadColor, RoadCongestedColor, Mathf.Clamp01(congestion));
    }

    public Color GetServiceColor(ServiceType serviceType)
    {
        return serviceType switch
        {
            ServiceType.Electricity => ElectricityColor,
            ServiceType.Water => WaterColor,
            ServiceType.Sewage => SewageColor,
            ServiceType.Waste => WasteColor,
            ServiceType.Fire => FireColor,
            ServiceType.Police => PoliceColor,
            ServiceType.Health => HealthColor,
            ServiceType.Education => EducationColor,
            _ => HudMutedTextColor
        };
    }

    public Color GetLotColor(ZoneLot lot)
    {
        var baseColor = GetZoneColor(lot.ZoneType);
        var utilityReadiness = (lot.HasElectricity ? 1f : 0f)
            + (lot.HasWater ? 1f : 0f)
            + (lot.HasSewage ? 1f : 0f)
            + (lot.HasRoadAccess ? 1f : 0f);
        var readiness = utilityReadiness / 4f;
        return Color.Lerp(new Color(0.37f, 0.39f, 0.42f, 0.26f), baseColor, Mathf.Clamp01(0.28f + (readiness * 0.72f)));
    }

    public Color GetBuildingColor(BuildingState building)
    {
        var baseColor = building.ServiceType != ServiceType.None
            ? GetServiceColor(building.ServiceType)
            : GetZoneColor(building.ZoneType);

        return Color.Lerp(baseColor, Color.white, Mathf.Clamp01(building.Level / 6f));
    }

    public Color GetHoverColor(PcToolMode toolMode)
    {
        return Color.Lerp(WithAlpha(GetToolColor(toolMode), HoverOutlineColor.a), HoverOutlineColor, 0.45f);
    }

    public Color GetDragFillColor(PcToolMode toolMode)
    {
        return Color.Lerp(WithAlpha(GetToolColor(toolMode), DragFillColor.a), DragFillColor, 0.35f);
    }

    public Color GetDragOutlineColor(PcToolMode toolMode)
    {
        return Color.Lerp(WithAlpha(GetToolColor(toolMode), DragOutlineColor.a), DragOutlineColor, 0.25f);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = alpha;
        return color;
    }
}
}
