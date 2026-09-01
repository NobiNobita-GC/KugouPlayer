namespace KugouPlayer.Models;

public sealed record AudioOutputDeviceModel(string Id, string DisplayName, bool IsDefault = false);
