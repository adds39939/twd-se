using System.Globalization;
using TwdSaveEditor.Tools.Common.Hashing;
using TwdSaveEditor.Tools.Common.Names;

if (args.Length < 2 || args[0] is not ("hash" or "find"))
{
    Console.WriteLine("Usage: Symbols hash <text>...");
    Console.WriteLine("       Symbols find <hex symbol>...");
    return 1;
}

if (args[0] == "hash")
{
    foreach (var text in args[1..])
    {
        Console.WriteLine($"{TelltaleCrc64.Compute(text):X16}  {text}");
    }

    return 0;
}

var names = SymbolNames.LoadDefault();
foreach (var value in args[1..])
{
    var hash = ulong.Parse(value.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? value[2..] : value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
    Console.WriteLine($"{hash:X16}  {names.Find(hash) ?? "?"}");
}

return 0;
