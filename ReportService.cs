using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace ComalaPageReport
{
    public class ReportService
    {
        private readonly AppSettings _settings;
        private readonly HttpClient _httpClient;
        private readonly JsonSerializerOptions _jsonOptions;
        private string _apiBasePath = "wiki/rest/api";
        private string _baseUrl = "";

        public ReportService(AppSettings settings)
        {
            _settings = settings;
            _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            _baseUrl = settings.BaseUrl.TrimEnd('/');
            _httpClient = new HttpClient();
            _httpClient.BaseAddress = new Uri(_baseUrl + "/");

            var credentials = Convert.ToBase64String(
                Encoding.ASCII.GetBytes($"{settings.Username}:{settings.ApiToken}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
            _httpClient.DefaultRequestHeaders.Accept
                .Add(new MediaTypeWithQualityHeaderValue("application/json"));
        }

        // ---------------------------------------------------------------
        // API-Pfad automatisch erkennen
        // ---------------------------------------------------------------
        private async Task<string> DetectApiBasePath()
        {
            var candidates = new[] { "wiki/rest/api", "rest/api", "confluence/rest/api" };

            Console.WriteLine("  Erkenne Confluence API-Pfad...");
            foreach (var path in candidates)
            {
                try
                {
                    var resp = await _httpClient.GetAsync($"{path}/space?limit=1");
                    Console.WriteLine($"    {path} -> HTTP {(int)resp.StatusCode}");
                    if (resp.IsSuccessStatusCode)
                    {
                        Console.WriteLine($"  -> Verwende: /{path}/");
                        return path;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"    {path} -> Fehler: {ex.Message}");
                }
            }

            throw new Exception(
                "\nConfluence REST API nicht erreichbar.\n" +
                $"  BaseUrl: '{_settings.BaseUrl}'\n" +
                "  Cloud:  Username = E-Mail, ApiToken = Token von id.atlassian.com/manage-profile/security/api-tokens\n" +
                "  Server: Username = Benutzername, ApiToken = Passwort");
        }

        // ---------------------------------------------------------------
        // Hauptlogik
        // ---------------------------------------------------------------
        public async Task RunAsync()
        {
            _apiBasePath = await DetectApiBasePath();
            Console.WriteLine();

            Console.WriteLine($"Lade alle Seiten aus Space '{_settings.SpaceKey}'...");
            var pages = await GetAllPagesInSpaceAsync(_settings.SpaceKey);
            Console.WriteLine($"  -> {pages.Count} Seite(n) gefunden.\n");

            if (pages.Count == 0)
            {
                Console.WriteLine("Keine Seiten gefunden. Bitte SpaceKey prüfen.");
                return;
            }

            var report = new List<PageReportEntry>();
            int current = 0;

            foreach (var page in pages)
            {
                current++;
                Console.Write($"  [{current}/{pages.Count}] \"{page.Title}\" ... ");

                var entry = new PageReportEntry
                {
                    Id    = page.Id,
                    Title = page.Title,
                    Url   = $"{_baseUrl}/wiki/spaces/{_settings.SpaceKey}/pages/{page.Id}"
                };

                // Comala-Status
                try
                {
                    entry.ComalaStatus = await GetComalaStatusAsync(page.Id) ?? "(kein Status)";
                }
                catch (Exception ex)
                {
                    entry.ComalaStatus = $"Fehler: {ex.Message}";
                }

                // Page Owner + letzte Änderung
                try
                {
                    var (ownerName, ownerEmail, lastBy, lastDate) = await GetPageInfoAsync(page.Id);
                    entry.OwnerDisplayName = ownerName;
                    entry.OwnerEmail       = ownerEmail;
                    entry.LastModifiedBy   = lastBy;
                    entry.LastModifiedDate = lastDate;
                }
                catch (Exception ex)
                {
                    entry.OwnerDisplayName = $"Fehler: {ex.Message}";
                }

                report.Add(entry);
                Console.WriteLine($"Status: \"{entry.ComalaStatus}\" | Owner: {entry.OwnerDisplayName}");
            }

            Console.WriteLine();

            // CSV exportieren
            var csvPath = ExportCsv(report);

            Console.WriteLine($"=== Fertig ===");
            Console.WriteLine($"  Seiten verarbeitet: {report.Count}");
            Console.WriteLine($"  Report gespeichert: {csvPath}");
            Console.WriteLine();

            PrintTable(report);
        }

        // ---------------------------------------------------------------
        // Alle Seiten eines Space laden (paginiert via start-Parameter)
        // ---------------------------------------------------------------
        private async Task<List<ConfluencePage>> GetAllPagesInSpaceAsync(string spaceKey)
        {
            var allPages = new List<ConfluencePage>();
            var seenIds  = new HashSet<string>();
            int start    = 0;
            int limit    = _settings.PageSizeLimit;

            while (true)
            {
                var url = $"{_apiBasePath}/content" +
                          $"?spaceKey={Uri.EscapeDataString(spaceKey)}" +
                          $"&type=page&status=current&start={start}&limit={limit}";

                var response = await _httpClient.GetAsync(url);

                if (!response.IsSuccessStatusCode)
                {
                    var body = await response.Content.ReadAsStringAsync();
                    throw new Exception(
                        $"HTTP {(int)response.StatusCode} beim Laden der Seiten.\n" +
                        $"  URL: {url}\n" +
                        $"  Antwort: {body.Substring(0, Math.Min(300, body.Length))}");
                }

                var json     = await response.Content.ReadAsStringAsync();
                var pageList = JsonSerializer.Deserialize<ConfluencePageList>(json, _jsonOptions);

                if (pageList?.Results == null || pageList.Results.Count == 0)
                    break;

                foreach (var page in pageList.Results)
                    if (seenIds.Add(page.Id))
                        allPages.Add(page);

                if (pageList.Results.Count < limit)
                    break;

                start += limit;
            }

            return allPages;
        }

        // ---------------------------------------------------------------
        // Comala-Status via Content Property "cw-status"
        // ---------------------------------------------------------------
        private async Task<string?> GetComalaStatusAsync(string pageId)
        {
            var url      = $"{_apiBasePath}/content/{pageId}/property/cw-status";
            var response = await _httpClient.GetAsync(url);

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                return null;

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync();
                throw new Exception($"HTTP {(int)response.StatusCode}: {body.Substring(0, Math.Min(200, body.Length))}");
            }

            var json = await response.Content.ReadAsStringAsync();

            if (_settings.DebugComalaResponse)
                Console.WriteLine($"\n    [Debug cw-status]: {json.Substring(0, Math.Min(500, json.Length))}");

            try
            {
                using var doc  = JsonDocument.Parse(json);
                var root       = doc.RootElement;

                if (root.TryGetProperty("value", out var valueProp))
                {
                    // value.state.name  <- Comala Cloud Hauptpfad
                    if (valueProp.TryGetProperty("state", out var stateProp))
                    {
                        if (stateProp.TryGetProperty("name", out var stateNameProp))
                            return stateNameProp.GetString();
                        if (stateProp.ValueKind == JsonValueKind.String)
                            return stateProp.GetString();
                    }

                    // value.name.value  <- alternatives Feld
                    if (valueProp.TryGetProperty("name", out var nameObjProp))
                    {
                        if (nameObjProp.TryGetProperty("value", out var nameValueProp))
                            return nameValueProp.GetString();
                        if (nameObjProp.ValueKind == JsonValueKind.String)
                            return nameObjProp.GetString();
                    }
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"JSON-Parse-Fehler: {ex.Message}");
            }

            return null;
        }

        // ---------------------------------------------------------------
        // Page Owner via Confluence v2 API + letzte Änderung
        // GET /wiki/api/v2/pages/{id}  ->  ownerId, version.authorId, version.createdAt
        // ---------------------------------------------------------------
        private async Task<(string ownerName, string ownerEmail, string lastModifiedBy, string lastModifiedDate)>
            GetPageInfoAsync(string pageId)
        {
            var v2Url      = $"wiki/api/v2/pages/{pageId}";
            var v2Response = await _httpClient.GetAsync(v2Url);

            string ownerId         = "";
            string lastModifiedBy  = "";
            string lastModifiedDate = "";

            if (v2Response.IsSuccessStatusCode)
            {
                var v2Json = await v2Response.Content.ReadAsStringAsync();
                using var v2Doc  = JsonDocument.Parse(v2Json);
                var v2Root = v2Doc.RootElement;

                // Page Owner
                if (v2Root.TryGetProperty("ownerId", out var ownerIdProp))
                    ownerId = ownerIdProp.GetString() ?? "";

                // Letzte Änderung
                if (v2Root.TryGetProperty("version", out var versionProp))
                {
                    if (versionProp.TryGetProperty("createdAt", out var dateProp))
                    {
                        var rawDate = dateProp.GetString() ?? "";
                        if (DateTime.TryParse(rawDate, out var dt))
                            lastModifiedDate = dt.ToString("dd.MM.yyyy HH:mm");
                    }
                    if (versionProp.TryGetProperty("authorId", out var authorIdProp))
                    {
                        var authorId = authorIdProp.GetString() ?? "";
                        if (!string.IsNullOrEmpty(authorId))
                            lastModifiedBy = await GetUserDisplayNameAsync(authorId);
                    }
                }
            }

            // Owner Name + E-Mail via User-API
            string ownerName  = "";
            string ownerEmail = "";
            if (!string.IsNullOrEmpty(ownerId))
                (ownerName, ownerEmail) = await GetUserInfoAsync(ownerId);

            return (ownerName, ownerEmail, lastModifiedBy, lastModifiedDate);
        }

        // ---------------------------------------------------------------
        // User Info (Name + E-Mail)
        // ---------------------------------------------------------------
        private async Task<(string displayName, string email)> GetUserInfoAsync(string accountId)
        {
            var url      = $"{_apiBasePath}/user?accountId={Uri.EscapeDataString(accountId)}";
            var response = await _httpClient.GetAsync(url);

            if (!response.IsSuccessStatusCode)
                return ("", "");

            var json = await response.Content.ReadAsStringAsync();
            try
            {
                using var doc = JsonDocument.Parse(json);
                var root  = doc.RootElement;
                var name  = root.TryGetProperty("displayName", out var n) ? n.GetString() ?? "" : "";
                var email = root.TryGetProperty("email",       out var e) ? e.GetString() ?? "" : "";
                return (name, email);
            }
            catch { }

            return ("", "");
        }

        private async Task<string> GetUserDisplayNameAsync(string accountId)
        {
            var (name, _) = await GetUserInfoAsync(accountId);
            return name;
        }

        // ---------------------------------------------------------------
        // CSV-Export
        // ---------------------------------------------------------------
        private string ExportCsv(List<PageReportEntry> entries)
        {
            // Ausgabepfad bestimmen
            var outputDir = string.IsNullOrWhiteSpace(_settings.OutputPath)
                ? AppContext.BaseDirectory
                : _settings.OutputPath.TrimEnd('\\', '/');

            // Verzeichnis anlegen falls nicht vorhanden
            if (!Directory.Exists(outputDir))
            {
                Directory.CreateDirectory(outputDir);
                Console.WriteLine($"  Verzeichnis erstellt: {outputDir}");
            }

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName  = $"comala_report_{_settings.SpaceKey}_{timestamp}.csv";
            var fullPath  = Path.Combine(outputDir, fileName);

            using var writer = new StreamWriter(fullPath, false, new UTF8Encoding(true));

            // Header
            writer.WriteLine("Titel;URL;Comala Status;Page Owner;Owner E-Mail;Zuletzt geändert von;Letzte Änderung");

            foreach (var e in entries)
            {
                writer.WriteLine(
                    $"{Escape(e.Title)};" +
                    $"{Escape(e.Url)};" +
                    $"{Escape(e.ComalaStatus)};" +
                    $"{Escape(e.OwnerDisplayName)};" +
                    $"{Escape(e.OwnerEmail)};" +
                    $"{Escape(e.LastModifiedBy)};" +
                    $"{Escape(e.LastModifiedDate)}");
            }

            return fullPath;
        }

        private static string Escape(string value)
        {
            if (string.IsNullOrEmpty(value)) return "";
            bool needsQuoting = value.Contains(';') || value.Contains('"') || value.Contains('\n');
            if (needsQuoting)
                return "\"" + value.Replace("\"", "\"\"") + "\"";
            return value;
        }

        // ---------------------------------------------------------------
        // Konsolenausgabe als Tabelle
        // ---------------------------------------------------------------
        private void PrintTable(List<PageReportEntry> entries)
        {
            if (entries.Count == 0) return;

            Console.WriteLine("=== Seitenübersicht ===");
            Console.WriteLine();

            int colTitle  = Math.Min(45, entries.Max(e => e.Title.Length)  + 2);
            int colStatus = Math.Min(30, entries.Max(e => e.ComalaStatus.Length) + 2);
            int colOwner  = Math.Min(25, entries.Max(e => e.OwnerDisplayName.Length) + 2);

            var header = $"  {"Titel".PadRight(colTitle)} {"Status".PadRight(colStatus)} {"Owner".PadRight(colOwner)} URL";
            Console.WriteLine(header);
            Console.WriteLine("  " + new string('-', Math.Min(header.Length, Console.WindowWidth - 3)));

            foreach (var e in entries)
            {
                var title  = Truncate(e.Title,            colTitle  - 1);
                var status = Truncate(e.ComalaStatus,     colStatus - 1);
                var owner  = Truncate(e.OwnerDisplayName, colOwner  - 1);

                Console.WriteLine($"  {title.PadRight(colTitle)} {status.PadRight(colStatus)} {owner.PadRight(colOwner)} {e.Url}");
            }

            Console.WriteLine();
        }

        private static string Truncate(string value, int maxLength)
        {
            if (string.IsNullOrEmpty(value)) return "";
            return value.Length <= maxLength ? value : value.Substring(0, maxLength - 3) + "...";
        }
    }
}
