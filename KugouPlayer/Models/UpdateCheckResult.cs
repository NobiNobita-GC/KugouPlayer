namespace KugouPlayer.Models;

public sealed record UpdateCheckResult(
    bool IsUpdateAvailable,
    string LatestVersion,
    string Message,
    Uri? ReleasePage = null);
