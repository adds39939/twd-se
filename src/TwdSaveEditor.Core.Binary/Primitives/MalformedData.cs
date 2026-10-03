namespace TwdSaveEditor.Core.Binary.Primitives;

internal static class MalformedData
{
    public static T Guard<T>(Func<T> read)
    {
        try
        {
            return read();
        }
        catch (Exception e) when (e is EndOfStreamException or ArgumentException or OverflowException or IndexOutOfRangeException)
        {
            throw new InvalidDataException(e.Message, e);
        }
    }
}
