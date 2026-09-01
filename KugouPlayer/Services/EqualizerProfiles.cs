namespace KugouPlayer.Services;

public static class EqualizerProfiles
{
    public static readonly float[] Frequencies = [31, 62, 125, 250, 500, 1000, 2000, 4000, 8000, 16000];

    private static readonly IReadOnlyDictionary<string, float[]> Profiles = new Dictionary<string, float[]>
    {
        ["关闭"] = [0, 0, 0, 0, 0, 0, 0, 0, 0, 0],
        ["流行"] = [-1, 1, 3, 4, 2, -1, -1, 2, 3, 2],
        ["摇滚"] = [4, 3, 2, 1, -1, -2, 0, 2, 3, 4],
        ["古典"] = [3, 2, 1, 0, -1, -1, 0, 1, 2, 3],
        ["人声"] = [-2, -1, 0, 2, 4, 5, 4, 2, 0, -1],
        ["低音增强"] = [6, 5, 4, 2, 0, -1, -1, 0, 1, 1]
    };

    public static IReadOnlyList<string> Names => Profiles.Keys.ToArray();

    public static float[] GetGains(string name) =>
        Profiles.TryGetValue(name, out var gains) ? gains.ToArray() : Profiles["关闭"].ToArray();
}
