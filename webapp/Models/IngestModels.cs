namespace Kiddo.Web.Models;

public record IngestGame(
    string ObjectId,
    string Slug,
    string Title,
    string? Company,
    string ThumbnailUrl,
    string? Description,
    string? Instruction,
    string[] Categories,
    string[] Tags,
    string[] Languages,
    string[] Gender,
    string[] AgeGroup,
    string? FirstActiveDate = null
);

public record IngestBatchRequest(IngestGame[] Games);
public record IngestResult(string ObjectId, bool Ok, string? Error = null);
public record IngestBatchResponse(IngestResult[] Results);
