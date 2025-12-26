namespace PrintControl.Host;

public static class ApiKeyValidator
{
    public static bool IsAuthorized(HttpRequest request, string? apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return true;
        }

        if (!request.Headers.TryGetValue("X-Api-Key", out var provided))
        {
            return false;
        }

        return string.Equals(provided.ToString(), apiKey, StringComparison.Ordinal);
    }
}
