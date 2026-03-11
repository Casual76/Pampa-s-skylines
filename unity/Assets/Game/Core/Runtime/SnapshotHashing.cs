#nullable enable

namespace PampaSkylines.Core
{
using System;
using System.Globalization;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

public static class SnapshotHashing
{
    public static string ComputeWorldHash(WorldState state)
    {
        var token = PampaSkylinesJson.ToToken(state);
        return ComputeSha256(StableStringify(token, sortArrays: true));
    }

    public static string ComputeContentHash(CitySnapshot snapshot)
    {
        var clone = new CitySnapshot
        {
            SchemaVersion = snapshot.SchemaVersion,
            CityId = snapshot.CityId,
            CityName = snapshot.CityName,
            Version = snapshot.Version,
            CreatedAtUtc = snapshot.CreatedAtUtc,
            SavedAtUtc = snapshot.SavedAtUtc,
            ClientId = snapshot.ClientId,
            CommandCount = snapshot.CommandCount,
            ContentHash = string.Empty,
            Metadata = snapshot.Metadata,
            State = snapshot.State
        };

        var token = PampaSkylinesJson.ToToken(clone);
        return ComputeSha256(StableStringify(token, sortArrays: true));
    }

    private static string StableStringify(JToken token, bool sortArrays)
    {
        return token.Type switch
        {
            JTokenType.Object => StableStringifyObject((JObject)token, sortArrays),
            JTokenType.Array => StableStringifyArray((JArray)token, sortArrays),
            JTokenType.Integer => Convert.ToString(((JValue)token).Value<long>(), CultureInfo.InvariantCulture) ?? "0",
            JTokenType.Float => FormatFloatingPoint(((JValue)token).Value<double>()),
            JTokenType.Boolean => ((JValue)token).Value<bool>() ? "true" : "false",
            JTokenType.Null => "null",
            JTokenType.Undefined => "null",
            JTokenType.String => JsonConvert.ToString(((JValue)token).Value<string>()),
            _ => token.ToString(Formatting.None)
        };
    }

    private static string StableStringifyObject(JObject jsonObject, bool sortArrays)
    {
        var serializedProperties = jsonObject.Properties()
            .OrderBy(property => property.Name, System.StringComparer.Ordinal)
            .Select(property => $"{JsonConvert.ToString(property.Name)}:{StableStringify(property.Value, sortArrays)}");

        return "{" + string.Join(",", serializedProperties) + "}";
    }

    private static string StableStringifyArray(JArray jsonArray, bool sortArrays)
    {
        var serializedItems = jsonArray
            .Select(item => StableStringify(item, sortArrays))
            .ToList();

        if (sortArrays)
        {
            serializedItems.Sort(System.StringComparer.Ordinal);
        }

        return "[" + string.Join(",", serializedItems) + "]";
    }

    private static string FormatFloatingPoint(double value)
    {
        if (double.IsNaN(value) || double.IsInfinity(value))
        {
            return "null";
        }

        return value.ToString("R", CultureInfo.InvariantCulture);
    }

    private static string ComputeSha256(string value)
    {
        var payload = Encoding.UTF8.GetBytes(value);
        using var sha256 = SHA256.Create();
        var hash = sha256.ComputeHash(payload);
        return ConvertToHex(hash);
    }

    private static string ConvertToHex(byte[] bytes)
    {
        var builder = new StringBuilder(bytes.Length * 2);
        foreach (var value in bytes)
        {
            builder.Append(value.ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
        }

        return builder.ToString();
    }
}
}
