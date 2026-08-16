using System.Text;

namespace Zenmanage.Flagging;

/// <summary>
/// Implements deterministic percentage rollout bucketing using CRC32B.
/// </summary>
public static class RolloutEvaluator
{
    private static readonly uint[] Crc32Table = BuildTable();

    public static uint Crc32B(string input)
    {
        var bytes = Encoding.UTF8.GetBytes(input);
        var crc = 0xffffffffu;

        foreach (var value in bytes)
        {
            crc = (crc >> 8) ^ Crc32Table[(crc ^ value) & 0xff];
        }

        return crc ^ 0xffffffffu;
    }

    public static bool IsInBucket(string salt, string? contextIdentifier, int percentage)
    {
        if (percentage < 0 || percentage > 100)
        {
            throw new ArgumentOutOfRangeException(nameof(percentage), "Percentage must be between 0 and 100");
        }

        if (contextIdentifier is null)
        {
            return false;
        }

        var hash = Crc32B($"{salt}:{contextIdentifier}");
        return hash % 100 < percentage;
    }

    private static uint[] BuildTable()
    {
        var table = new uint[256];
        for (uint i = 0; i < table.Length; i++)
        {
            var crc = i;
            for (var j = 0; j < 8; j++)
            {
                crc = (crc & 1) == 1 ? (crc >> 1) ^ 0xedb88320u : crc >> 1;
            }

            table[i] = crc;
        }

        return table;
    }
}