using System;
using Newtonsoft.Json;

namespace ApexLauncherSystem.Models;

public sealed class GameModel
{
    [JsonProperty("id")]
    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    [JsonProperty("name")]
    public string Name { get; set; } = string.Empty;

    [JsonProperty("description")]
    public string Description { get; set; } = string.Empty;

    [JsonProperty("exePath")]
    public string ExePath { get; set; } = string.Empty;

    [JsonProperty("workingDirectory")]
    public string WorkingDirectory { get; set; } = string.Empty;

    [JsonProperty("imagePath")]
    public string ImagePath { get; set; } = string.Empty;

    [JsonProperty("categoryId")]
    public string CategoryId { get; set; } = string.Empty;

    [JsonProperty("launchCount")]
    public int LaunchCount { get; set; }

    [JsonProperty("lastPlayed")]
    public DateTime? LastPlayed { get; set; }

    [JsonProperty("isFeatured")]
    public bool IsFeatured { get; set; }
}
