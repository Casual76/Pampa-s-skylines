namespace PampaSkylines.PC
{
using PampaSkylines.Core;
using UnityEngine;

public enum PcToolMode
{
    Road = 0,
    Residential = 1,
    Commercial = 2,
    Industrial = 3,
    Office = 4,
    Electricity = 5,
    Water = 6,
    Sewage = 7,
    Waste = 8,
    Fire = 9,
    Police = 10,
    Health = 11,
    Education = 12,
    Bulldoze = 13
}

public static class PcToolModeExtensions
{
    public static bool IsDragTool(this PcToolMode toolMode)
    {
        return toolMode == PcToolMode.Road || toolMode.IsZoneTool();
    }

    public static bool IsZoneTool(this PcToolMode toolMode)
    {
        return toolMode is PcToolMode.Residential
            or PcToolMode.Commercial
            or PcToolMode.Industrial
            or PcToolMode.Office;
    }

    public static bool IsServiceTool(this PcToolMode toolMode)
    {
        return toolMode is PcToolMode.Electricity
            or PcToolMode.Water
            or PcToolMode.Sewage
            or PcToolMode.Waste
            or PcToolMode.Fire
            or PcToolMode.Police
            or PcToolMode.Health
            or PcToolMode.Education;
    }

    public static string ToDisplayName(this PcToolMode toolMode)
    {
        return toolMode switch
        {
            PcToolMode.Road => "Strada",
            PcToolMode.Residential => "Residenziale",
            PcToolMode.Commercial => "Commerciale",
            PcToolMode.Industrial => "Industriale",
            PcToolMode.Office => "Uffici",
            PcToolMode.Electricity => "Elettricita",
            PcToolMode.Water => "Acqua",
            PcToolMode.Sewage => "Fogne",
            PcToolMode.Waste => "Rifiuti",
            PcToolMode.Fire => "Vigili",
            PcToolMode.Police => "Polizia",
            PcToolMode.Health => "Sanita",
            PcToolMode.Education => "Istruzione",
            PcToolMode.Bulldoze => "Bulldozer",
            _ => toolMode.ToString()
        };
    }

    public static ZoneType ToZoneType(this PcToolMode toolMode)
    {
        return toolMode switch
        {
            PcToolMode.Residential => ZoneType.Residential,
            PcToolMode.Commercial => ZoneType.Commercial,
            PcToolMode.Industrial => ZoneType.Industrial,
            PcToolMode.Office => ZoneType.Office,
            _ => ZoneType.None
        };
    }

    public static ServiceType ToServiceType(this PcToolMode toolMode)
    {
        return toolMode switch
        {
            PcToolMode.Electricity => ServiceType.Electricity,
            PcToolMode.Water => ServiceType.Water,
            PcToolMode.Sewage => ServiceType.Sewage,
            PcToolMode.Waste => ServiceType.Waste,
            PcToolMode.Fire => ServiceType.Fire,
            PcToolMode.Police => ServiceType.Police,
            PcToolMode.Health => ServiceType.Health,
            PcToolMode.Education => ServiceType.Education,
            _ => ServiceType.None
        };
    }

    public static Color ToColor(this PcToolMode toolMode)
    {
        return toolMode switch
        {
            PcToolMode.Road => new Color(0.22f, 0.25f, 0.29f),
            PcToolMode.Residential => new Color(0.34f, 0.71f, 0.36f),
            PcToolMode.Commercial => new Color(0.25f, 0.63f, 0.83f),
            PcToolMode.Industrial => new Color(0.74f, 0.58f, 0.21f),
            PcToolMode.Office => new Color(0.28f, 0.48f, 0.78f),
            PcToolMode.Electricity => new Color(0.91f, 0.68f, 0.17f),
            PcToolMode.Water => new Color(0.18f, 0.58f, 0.83f),
            PcToolMode.Sewage => new Color(0.43f, 0.53f, 0.25f),
            PcToolMode.Waste => new Color(0.52f, 0.45f, 0.19f),
            PcToolMode.Fire => new Color(0.82f, 0.24f, 0.21f),
            PcToolMode.Police => new Color(0.20f, 0.30f, 0.67f),
            PcToolMode.Health => new Color(0.16f, 0.63f, 0.48f),
            PcToolMode.Education => new Color(0.70f, 0.40f, 0.19f),
            PcToolMode.Bulldoze => new Color(0.66f, 0.18f, 0.16f),
            _ => Color.white
        };
    }
}
}
