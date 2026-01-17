namespace PacDancegameDemo.Models;

public sealed class SongItem
{
    public required string Name { get; init; }
    public required string FilePath { get; init; }
    public string? ImagePath { get; init; }
    public List<string> BackgroundImages { get; init; } = new();
}
