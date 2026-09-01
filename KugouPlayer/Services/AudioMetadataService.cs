using System.IO;

namespace KugouPlayer.Services;

public sealed record AudioMetadata(string? Title, string? Artist, string? Album, TimeSpan Duration, byte[]? CoverData);

public sealed class AudioMetadataService
{
    public AudioMetadata? Read(string filePath)
    {
        try
        {
            using var mediaFile = TagLib.File.Create(filePath);
            var picture = mediaFile.Tag.Pictures.FirstOrDefault();
            return new AudioMetadata(
                mediaFile.Tag.Title,
                mediaFile.Tag.Performers.FirstOrDefault(),
                mediaFile.Tag.Album,
                mediaFile.Properties.Duration,
                picture?.Data.Data);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or TagLib.CorruptFileException or TagLib.UnsupportedFormatException)
        {
            return null;
        }
    }
}

