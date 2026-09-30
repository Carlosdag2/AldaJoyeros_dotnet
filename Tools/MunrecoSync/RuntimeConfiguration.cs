using System.Text.Json;

static class RuntimeConfiguration {
    public static async Task<JsonDocument> Read(string path) => JsonDocument.Parse(
        Environment.GetEnvironmentVariable("ALDA_MUNRECO_CONNECTIONS") ?? await File.ReadAllTextAsync(path),
        new(){CommentHandling=JsonCommentHandling.Skip,AllowTrailingCommas=true});
}
