using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ComalaPageReport
{
    // --- Confluence REST API ---

    public class ConfluencePageList
    {
        [JsonPropertyName("results")]
        public List<ConfluencePage> Results { get; set; } = new();

        [JsonPropertyName("size")]
        public int Size { get; set; }
    }

    public class ConfluencePage
    {
        [JsonPropertyName("id")]
        public string Id { get; set; } = "";

        [JsonPropertyName("title")]
        public string Title { get; set; } = "";
    }

    public class ConfluenceUser
    {
        [JsonPropertyName("displayName")]
        public string DisplayName { get; set; } = "";

        [JsonPropertyName("accountId")]
        public string AccountId { get; set; } = "";

        [JsonPropertyName("email")]
        public string Email { get; set; } = "";
    }

    // --- Report ---

    public class PageReportEntry
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Url { get; set; } = "";
        public string ComalaStatus { get; set; } = "";
        public string OwnerDisplayName { get; set; } = "";
        public string OwnerEmail { get; set; } = "";
        public string LastModifiedBy { get; set; } = "";
        public string LastModifiedDate { get; set; } = "";
    }
}
