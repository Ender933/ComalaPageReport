namespace ComalaPageReport
{
    public class AppSettings
    {
        public string BaseUrl { get; set; } = "";
        public string Username { get; set; } = "";
        public string ApiToken { get; set; } = "";
        public string SpaceKey { get; set; } = "";

        /// <summary>
        /// Verzeichnis für den CSV-Export.
        /// Beispiel: "C:\\temp" oder "C:\\Reports\\Confluence"
        /// Leer lassen = Programmverzeichnis wird verwendet.
        /// </summary>
        public string OutputPath { get; set; } = "";

        public int PageSizeLimit { get; set; } = 50;
        public bool DebugComalaResponse { get; set; } = false;
    }
}
