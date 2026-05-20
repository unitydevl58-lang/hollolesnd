using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

/// <summary>
/// JSON command parser that accepts English fields and legacy Turkish aliases.
/// </summary>
public sealed class JsonGeometryCommandParser : ICommandParser
{
    private readonly IShapeParser shapeParser;
    private readonly JsonPayloadExtractor payloadExtractor;
    private readonly GeometryGenerationSettings settings;

    public JsonGeometryCommandParser(IShapeParser shapeParser, JsonPayloadExtractor payloadExtractor, GeometryGenerationSettings settings)
    {
        this.shapeParser = shapeParser;
        this.payloadExtractor = payloadExtractor;
        this.settings = settings;
    }

    public CommandParseResult Parse(string rawCommandData)
    {
        if (string.IsNullOrWhiteSpace(rawCommandData))
            return CommandParseResult.Failed("Empty JSON command received.");

        try
        {
            string json = payloadExtractor.Extract(rawCommandData);
            JToken root = JToken.Parse(json);

            if (root.Type == JTokenType.Array)
                return CommandParseResult.Succeeded(ParseArray((JArray)root));

            if (root.Type == JTokenType.Object)
            {
                JObject rootObject = (JObject)root;
                JToken wrappedCommands = JsonFieldReader.GetProperty(rootObject, "commands", "objects", "items", "komutlar", "nesneler");
                if (wrappedCommands is JArray wrappedArray)
                    return CommandParseResult.Succeeded(ParseArray(wrappedArray));

                return CommandParseResult.Succeeded(new List<GeometryCommand> { ParseObject(rootObject) });
            }

            return CommandParseResult.Failed("JSON root must be an object or an array.");
        }
        catch (JsonException exception)
        {
            return CommandParseResult.Failed($"JSON parse failed: {exception.Message}");
        }
        catch (Exception exception)
        {
            return CommandParseResult.Failed($"Command parse failed: {exception.Message}");
        }
    }

    private List<GeometryCommand> ParseArray(JArray array)
    {
        List<GeometryCommand> commands = new List<GeometryCommand>();

        for (int index = 0; index < array.Count; index++)
        {
            if (array[index] is JObject commandObject)
                commands.Add(ParseObject(commandObject));
        }

        return commands;
    }

    private GeometryCommand ParseObject(JObject source)
    {
        string shapeName = JsonFieldReader.ReadString(source, "shape", "type", "primitive", "sekil", "şekil");
        if (!shapeParser.TryParse(shapeName, out VoxelShape shape))
            shape = VoxelShape.Cube;

        int defaultSubdivision = shape == VoxelShape.Cube ? settings.MinSubdivision : settings.MinCurvedSubdivision;

        return new GeometryCommand
        {
            Action = JsonFieldReader.ReadString(source, "action", "command", "komut") ?? "create",
            Shape = shape,
            Subdivision = JsonFieldReader.ReadInt(source, defaultSubdivision, "subdivision", "divisions", "segments", "bolunme", "bölünme"),
            ColorValue = JsonFieldReader.ReadString(source, "color", "colour", "renk") ?? "white",
            PositionOffset = JsonFieldReader.ReadVector3(source, "position", "positionOffset", "offset", "konum"),
            Scale = JsonFieldReader.ReadFloat(source, 1f, "scale", "size", "olcek", "ölçek"),
            BinaryPartition = ReadBinaryPartition(source)
        };
    }

    private BinaryPartitionInstruction ReadBinaryPartition(JObject source)
    {
        JToken token = JsonFieldReader.GetProperty(source, "partition", "binaryPartition", "binarySplit", "split", "ayir", "ayır");
        JToken axisToken = JsonFieldReader.GetProperty(source, "partitionAxis", "splitAxis", "axis", "eksen");

        if (token == null && axisToken == null)
            return BinaryPartitionInstruction.None();

        BinaryPartitionInstruction instruction = new BinaryPartitionInstruction
        {
            Enabled = IsPartitionEnabled(token) || axisToken != null,
            Axis = ParseAxis(axisToken?.ToString(), PartitionAxis.X),
            Gap = settings.DefaultPartitionGap
        };

        if (token is JObject partitionObject)
        {
            string mode = JsonFieldReader.ReadString(partitionObject, "mode", "type", "kind");
            instruction.Enabled = JsonFieldReader.ReadBool(partitionObject, IsBinaryMode(mode), "enabled", "active");
            instruction.Axis = ParseAxis(JsonFieldReader.ReadString(partitionObject, "axis", "eksen"), instruction.Axis);
            instruction.Gap = Mathf.Max(0f, JsonFieldReader.ReadFloat(partitionObject, settings.DefaultPartitionGap, "gap", "spacing", "bosluk", "boşluk"));
            instruction.FirstColor = JsonFieldReader.ReadString(partitionObject, "firstColor", "leftColor", "primaryColor");
            instruction.SecondColor = JsonFieldReader.ReadString(partitionObject, "secondColor", "rightColor", "secondaryColor");

            JToken colors = JsonFieldReader.GetProperty(partitionObject, "colors", "colours", "renkler");
            if (colors is JArray colorArray && colorArray.Count >= 2)
            {
                instruction.FirstColor = colorArray[0]?.ToString();
                instruction.SecondColor = colorArray[1]?.ToString();
            }
        }

        return instruction.Enabled ? instruction : BinaryPartitionInstruction.None();
    }

    private bool IsPartitionEnabled(JToken token)
    {
        if (token == null)
            return false;

        if (token.Type == JTokenType.Boolean)
            return token.Value<bool>();

        if (token.Type == JTokenType.Object)
            return true;

        return IsBinaryMode(token.ToString());
    }

    private bool IsBinaryMode(string rawMode)
    {
        string normalized = TextNormalizer.NormalizeKey(rawMode);
        return normalized == "binary"
            || normalized == "split"
            || normalized == "partition"
            || normalized == "two"
            || normalized == "2"
            || normalized == "2ye"
            || normalized == "ikiye"
            || normalized == "ayir"
            || normalized == "bol";
    }

    private PartitionAxis ParseAxis(string rawAxis, PartitionAxis fallback)
    {
        string normalized = TextNormalizer.NormalizeKey(rawAxis);
        switch (normalized)
        {
            case "x":
            case "horizontal":
            case "yatay":
            case "left":
            case "right":
            case "sol":
            case "sag":
                return PartitionAxis.X;

            case "y":
            case "vertical":
            case "dikey":
            case "up":
            case "down":
            case "yukari":
            case "asagi":
                return PartitionAxis.Y;

            case "z":
            case "depth":
            case "forward":
            case "back":
            case "ileri":
            case "geri":
                return PartitionAxis.Z;

            default:
                return fallback;
        }
    }
}
