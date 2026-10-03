using TwdSaveEditor.Tools.Common.EventLog;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.FinalValidation.Parsing;

namespace TwdSaveEditor.Tools.FinalValidation.Steps;

public sealed class Crc64Step(ValidationContext context) : IValidationStep
{
    private static readonly (string Text, ulong Expected)[] EventTypes =
    [
        ("Executing Dialog Node", EventLogFormat.ExecutingDialogNode),
        ("Dialog Choice", EventLogFormat.DialogChoice),
        ("Begin Episode", EventLogFormat.BeginEpisode),
        ("End Episode", EventLogFormat.EndEpisode),
        ("Save Serial", EventLogFormat.SaveSerial),
    ];

    private static readonly (string Name, string Description)[] Types =
    [
        ("bool", "Bool"),
        ("int32", "Int32"),
        ("float", "Float"),
        ("String", "String"),
        ("Symbol", "Symbol"),
        ("Flags", "Flags"),
        ("PropertySet", "PropertySet"),
    ];

    public void Run()
    {
        context.Report.StepHeader("STEP 10: Validate CRC64 hash function consistency");
        var details = new List<string>();
        var passed = true;

        foreach (var (text, expected) in EventTypes)
        {
            var computed = TelltaleCrc64.Compute(text);
            var match = computed == expected;
            details.Add($"CRC64(\"{text}\") = 0x{computed:X16} {(match ? "==" : "!=")} 0x{expected:X16} {(match ? "OK" : "MISMATCH")}");
            if (!match)
            {
                passed = false;
            }
        }

        details.Add("\nChoicesContainer hash check:");
        details.Add($"  CRC64(\"DCArray<ChoiceData>\") = 0x{TelltaleCrc64.Compute("DCArray<ChoiceData>"):X16}");
        details.Add($"  Expected (C#): 0x{ChoicesContainerScanner.TypeHash:X16}");

        foreach (var (name, description) in Types)
        {
            details.Add($"  CRC64(\"{name}\") = 0x{TelltaleCrc64.Compute(name):X16}  ({description})");
        }

        context.Report.Add("10. CRC64 hash function", passed, details);
    }
}
