using KugouPlayer.Models;
using System.IO;
using System.Text.Json;

namespace KugouPlayer.Services;

public sealed class LibraryStateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly string _stateFilePath;

    public LibraryStateService()
    {
        var appDirectory = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "KugouPlayer");
        _stateFilePath = Path.Combine(appDirectory, "library.json");
    }

    public LibrarySnapshot Load()
    {
        try
        {
            if (!File.Exists(_stateFilePath))
            {
                return new LibrarySnapshot();
            }

            return JsonSerializer.Deserialize<LibrarySnapshot>(File.ReadAllText(_stateFilePath), JsonOptions) ?? new LibrarySnapshot();
        }
        catch (JsonException)
        {
            return new LibrarySnapshot();
        }
        catch (IOException)
        {
            return new LibrarySnapshot();
        }
    }

    public void Save(LibrarySnapshot snapshot)
    {
        try
        {
            var directory = Path.GetDirectoryName(_stateFilePath)!;
            Directory.CreateDirectory(directory);
            var temporaryPath = _stateFilePath + ".tmp";
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(snapshot, JsonOptions));
            File.Move(temporaryPath, _stateFilePath, true);
        }
        catch (IOException)
        {
            // 播放体验不应因状态文件暂时不可写而中断。
        }
        catch (UnauthorizedAccessException)
        {
            // 便携运行环境可能禁止写入 LocalAppData。
        }
    }
}
