namespace Kiddo.Web.Models;

public class Game
{
    public string Id { get; set; } = "";
    public string ObjectId { get; set; } = "";
    public string Slug { get; set; } = "";
    public string Title { get; set; } = "";
    public string? Company { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? Description { get; set; }
    public string? Instruction { get; set; }
    public string? DescriptionTh { get; set; }
    public string? InstructionTh { get; set; }
    public string[] Categories { get; set; } = [];
    public string[] Tags { get; set; } = [];
    public string[] Languages { get; set; } = [];
    public string[] Gender { get; set; } = [];
    public string[] AgeGroup { get; set; } = [];
    public string Status { get; set; } = "";
    public int ViewCount { get; set; }
    public DateTime CreatedAt { get; set; }

    public string GameUrl => $"https://html5.gamedistribution.com/{ObjectId}/?gd_sdk_referrer_url=https://gamedistribution.com/games/{Slug}/";
}
