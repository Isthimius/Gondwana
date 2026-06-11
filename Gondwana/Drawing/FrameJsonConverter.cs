using Gondwana.Drawing.Tilesheets;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Gondwana.Drawing;

/// <summary>
/// Serializes a Frame as a lightweight tilesheet/region/coordinate reference
/// instead of serializing the full Tilesheet object graph.
/// </summary>
internal sealed class FrameJsonConverter : JsonConverter<Frame>
{
    public override void WriteJson(
        JsonWriter writer,
        Frame value,
        JsonSerializer serializer)
    {
        if (value.Tilesheet is null)
        {
            writer.WriteNull();
            return;
        }

        writer.WriteStartObject();

        writer.WritePropertyName("tilesheet");
        writer.WriteValue(value.Tilesheet.Name);

        writer.WritePropertyName("regionName");
        writer.WriteValue(value.RegionName);

        writer.WritePropertyName("xTile");
        writer.WriteValue(value.XTile);

        writer.WritePropertyName("yTile");
        writer.WriteValue(value.YTile);

        writer.WriteEndObject();
    }

    public override Frame ReadJson(
        JsonReader reader,
        Type objectType,
        Frame existingValue,
        bool hasExistingValue,
        JsonSerializer serializer)
    {
        if (reader.TokenType == JsonToken.Null)
            return default;

        var obj = JObject.Load(reader);

        var tilesheetName = obj.Value<string>("tilesheet");
        var regionName = obj.Value<string>("regionName")
            ?? TilesheetRegion.DefaultRegionName;

        var xTile = obj.Value<int?>("xTile") ?? 0;
        var yTile = obj.Value<int?>("yTile") ?? 0;

        if (string.IsNullOrWhiteSpace(tilesheetName))
            throw new JsonSerializationException("Frame is missing required tilesheet name.");

        var tilesheet = TilesheetRegistry.Instance.GetOrNull(tilesheetName);

        if (tilesheet is null)
        {
            throw new JsonSerializationException(
                $"Could not resolve Tilesheet '{tilesheetName}' while deserializing Frame.");
        }

        return new Frame(
            tilesheet,
            regionName,
            xTile,
            yTile);
    }
}