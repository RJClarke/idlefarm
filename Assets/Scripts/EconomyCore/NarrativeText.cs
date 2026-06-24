/// <summary>Resolves narrative text tokens. Currently just {farmName}; structured so
/// adding tokens is a one-line change.</summary>
public static class NarrativeText
{
    public static string Resolve(string body, string farmName)
    {
        if (string.IsNullOrEmpty(body)) return "";
        return body.Replace("{farmName}", farmName ?? "");
    }
}
