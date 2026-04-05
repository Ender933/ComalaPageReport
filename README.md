# ComalaPageReport

Liest alle Seiten eines Confluence-Space aus und erstellt einen CSV-Report mit:
- Seitentitel
- URL
- Comala Workflow-Status
- Page Owner (Name + E-Mail)
- Zuletzt geändert von
- Datum der letzten Änderung

Im Gegensatz zum ComalaStatusChecker werden **alle** Seiten unabhängig vom Status erfasst.
Es werden keine Labels gesetzt oder Änderungen vorgenommen – reines Lesen.

## Voraussetzungen

- .NET 8 SDK (https://dotnet.microsoft.com/download)
- Confluence Cloud mit installiertem Comala Document Management
- API-Token: https://id.atlassian.com/manage-profile/security/api-tokens

## Konfiguration (appsettings.json)

```json
{
  "Confluence": {
    "BaseUrl":    "https://firma.atlassian.net",
    "Username":   "ihre-email@firma.de",
    "ApiToken":   "IHR_API_TOKEN",
    "SpaceKey":   "QMVA",
    "OutputPath": "C:\\temp",
    "PageSizeLimit": 50,
    "DebugComalaResponse": false
  }
}
```

| Einstellung           | Beschreibung                                                        |
|-----------------------|---------------------------------------------------------------------|
| `BaseUrl`             | Confluence-URL ohne abschliessendes /                               |
| `Username`            | E-Mail-Adresse (Confluence Cloud)                                   |
| `ApiToken`            | API-Token von id.atlassian.com                                      |
| `SpaceKey`            | Space-Key, z.B. QMVA                                                |
| `OutputPath`          | Ausgabeverzeichnis für CSV. Leer = Programmverzeichnis              |
| `PageSizeLimit`       | Seiten pro API-Anfrage (Standard: 50, max. 100)                     |
| `DebugComalaResponse` | true = rohe Comala API-Antwort in Konsole ausgeben                  |

## Starten

```bash
cd ComalaPageReport
dotnet run
```

## Ausgabe

Der CSV wird im konfigurierten `OutputPath` gespeichert:
```
C:\temp\comala_report_QMVA_20260330_143022.csv
```

Das Trennzeichen ist `;` (Semikolon) – direkt in Excel öffenbar.

### CSV-Spalten

| Spalte                | Inhalt                                      |
|-----------------------|---------------------------------------------|
| Titel                 | Seitentitel                                 |
| URL                   | Direktlink zur Confluence-Seite             |
| Comala Status         | Aktueller Workflow-Status oder "(kein Status)" |
| Page Owner            | Anzeigename des eingetragenen Page Owners   |
| Owner E-Mail          | E-Mail des Page Owners (sofern sichtbar)    |
| Zuletzt geändert von  | Anzeigename des letzten Bearbeiters         |
| Letzte Änderung       | Datum + Uhrzeit der letzten Änderung        |

## Hinweis E-Mail

Confluence Cloud gibt E-Mails nur zurück wenn der API-Token-Inhaber
die Berechtigung "View user email addresses" hat (Admin oder entsprechende Rolle).
Falls die Spalte leer bleibt, liegt das an Confluence-Berechtigungen.
