
using System.Collections.Generic;
using System.IO;

namespace ColdChainMonitor;

public static class ReadingFileStream
{
    public static IEnumerable<Reading> ReadValidReadings(string path)
    {
        using var reader = new StreamReader(path);

        string? line;

        while ((line = reader.ReadLine()) != null)
        {
            if (ColdChainProcessor.TryParseReading(
                line,
                out Reading? reading,
                out _))
            {
                yield return reading!;
            }
        }
    }
}
