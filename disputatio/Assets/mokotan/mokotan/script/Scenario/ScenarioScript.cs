using System;
using System.Collections.Generic;
using UnityEngine;

[Serializable]
public sealed class ScenarioScript
{
    public int schema_version = 1;
    public ScenarioBlock[] blocks = Array.Empty<ScenarioBlock>();

    public static ScenarioScript FromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return new ScenarioScript();

        ScenarioScript script = JsonUtility.FromJson<ScenarioScript>(json);
        return script ?? new ScenarioScript();
    }

    public bool TryGetBlock(string blockId, out ScenarioBlock block)
    {
        block = null;
        if (string.IsNullOrWhiteSpace(blockId) || blocks == null)
            return false;

        foreach (ScenarioBlock candidate in blocks)
        {
            if (candidate != null && string.Equals(candidate.block_id, blockId, StringComparison.Ordinal))
            {
                block = candidate;
                return true;
            }
        }

        return false;
    }
}

[Serializable]
public sealed class ScenarioBlock
{
    public string block_id;
    public ScenarioCommand[] commands = Array.Empty<ScenarioCommand>();
}

[Serializable]
public sealed class ScenarioCommand
{
    public string command = "talk_standing";
    public string line_id;
    public string speaker_id;
    public string side = "left";
    public string speaker_sprite;
    public string other_sprite;
    public string say_dialog;
}

public enum ScenarioSpeakerSide
{
    Left,
    Right
}

public sealed class ScenarioTalkLine
{
    public string lineId;
    public string speakerId;
    public string speakerName;
    public string text;
    public ScenarioSpeakerSide side;
    public string speakerSprite;
    public string otherSprite;
    public string sayDialog;
}

public static class ScenarioBlockResolver
{
    public static ScenarioTalkLine[] BuildTalkLines(
        ScenarioScript script,
        string blockId,
        ScenarioLocalizationTable dialogue,
        ScenarioLocalizationTable speakers)
    {
        if (script == null || !script.TryGetBlock(blockId, out ScenarioBlock block) || block.commands == null)
            return Array.Empty<ScenarioTalkLine>();

        var lines = new List<ScenarioTalkLine>();
        foreach (ScenarioCommand command in block.commands)
        {
            if (command == null || !IsTalkStanding(command.command))
                continue;

            lines.Add(new ScenarioTalkLine
            {
                lineId = command.line_id,
                speakerId = command.speaker_id,
                speakerName = speakers?.Get(command.speaker_id) ?? command.speaker_id ?? string.Empty,
                text = dialogue?.Get(command.line_id) ?? command.line_id ?? string.Empty,
                side = ParseSide(command.side),
                speakerSprite = command.speaker_sprite,
                otherSprite = command.other_sprite,
                sayDialog = command.say_dialog
            });
        }

        return lines.ToArray();
    }

    private static bool IsTalkStanding(string command)
    {
        return string.IsNullOrWhiteSpace(command)
            || string.Equals(command, "talk_standing", StringComparison.OrdinalIgnoreCase)
            || string.Equals(command, "Talk Standing", StringComparison.OrdinalIgnoreCase);
    }

    private static ScenarioSpeakerSide ParseSide(string side)
    {
        return string.Equals(side, "right", StringComparison.OrdinalIgnoreCase)
            ? ScenarioSpeakerSide.Right
            : ScenarioSpeakerSide.Left;
    }
}
