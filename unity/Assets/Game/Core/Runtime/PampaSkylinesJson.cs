namespace PampaSkylines.Core
{
using System.Globalization;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;
using Newtonsoft.Json.Linq;
using Newtonsoft.Json.Serialization;

public static class PampaSkylinesJson
{
    private static readonly DefaultContractResolver ContractResolver = new()
    {
        NamingStrategy = new CamelCaseNamingStrategy()
    };

    private static readonly JsonSerializerSettings DefaultSettings = CreateSettings(Formatting.None, includeStringEnums: false);
    private static readonly JsonSerializerSettings IndentedSettings = CreateSettings(Formatting.Indented, includeStringEnums: false);
    private static readonly JsonSerializerSettings CatalogSettings = CreateSettings(Formatting.None, includeStringEnums: true);

    public static string Serialize(object value)
    {
        return JsonConvert.SerializeObject(value, DefaultSettings);
    }

    public static string SerializeIndented(object value)
    {
        return JsonConvert.SerializeObject(value, IndentedSettings);
    }

    public static T Deserialize<T>(string json)
        where T : class
    {
        return JsonConvert.DeserializeObject<T>(json, DefaultSettings);
    }

    public static T DeserializeCatalog<T>(string json)
        where T : class
    {
        return JsonConvert.DeserializeObject<T>(json, CatalogSettings);
    }

    public static JToken ToToken(object value)
    {
        return Parse(Serialize(value));
    }

    public static JToken Parse(string json)
    {
        using var stringReader = new StringReader(json);
        using var jsonReader = new JsonTextReader(stringReader)
        {
            DateParseHandling = DateParseHandling.None,
            FloatParseHandling = FloatParseHandling.Double
        };

        return JToken.ReadFrom(jsonReader);
    }

    private static JsonSerializerSettings CreateSettings(Formatting formatting, bool includeStringEnums)
    {
        var settings = new JsonSerializerSettings
        {
            ContractResolver = ContractResolver,
            Formatting = formatting,
            Culture = CultureInfo.InvariantCulture,
            DateParseHandling = DateParseHandling.None,
            NullValueHandling = NullValueHandling.Include
        };

        if (includeStringEnums)
        {
            settings.Converters.Add(new StringEnumConverter());
        }

        return settings;
    }
}
}
