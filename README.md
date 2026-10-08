# Link Analytics API

A REST API for shortening URLs and tracking how often each short link is accessed, built with C# and ASP.NET Core Minimal APIs.

You submit a long URL and get a short code back. Visiting `/r/{code}` redirects to the original URL and records the access, and `/api/links/{code}/stats` shows how many times the link was used, when it was last used and how accesses are distributed per day.

## Features

- Create short links for HTTP/HTTPS URLs, with input validation
- Look up the details of a short link
- Redirect from a short code to the original URL (`302 Found`, `Cache-Control: no-store`)
- Access statistics per link: total accesses, last access and accesses per day (UTC)
- Privacy-friendly: no IP address, location, user agent or other visitor data is stored
- Random, non-sequential 7-character Base62 codes, guaranteed unique by the database
- Short URLs built from a configured public base URL, never from the request `Host` header
- Errors returned as [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457)
- OpenAPI 3.1 document in development

## Tech stack

- [.NET 10](https://dotnet.microsoft.com/) (LTS) / ASP.NET Core Minimal APIs
- Entity Framework Core 10 + SQLite, with migrations
- `Microsoft.AspNetCore.OpenApi` for OpenAPI document generation
- xUnit + `WebApplicationFactory` for integration tests

## Project structure

```
.
├── LinkAnalytics.slnx                # Solution file
├── global.json                       # Pins the .NET SDK version
├── dotnet-tools.json                 # Local tools (dotnet-ef)
├── src/
│   └── LinkAnalytics.Api/
│       ├── Contracts/                # Request/response DTOs
│       ├── Data/                     # DbContext and EF Core migrations
│       ├── Endpoints/                # Endpoint definitions grouped by feature
│       ├── Models/                   # Entities (ShortLink, LinkClick)
│       ├── Services/                 # Business logic, options and validation
│       ├── appsettings*.json         # Configuration
│       ├── LinkAnalytics.Api.http    # Request examples
│       └── Program.cs                # App configuration and pipeline
└── tests/
    └── LinkAnalytics.Tests/
        ├── Endpoints/                # Integration tests
        ├── Infrastructure/           # Test host with an isolated database
        └── Services/                 # Unit tests
```

## Running locally

### Prerequisites

- [.NET SDK 10.0](https://dotnet.microsoft.com/download/dotnet/10.0) (10.0.301 or later)

### Steps

From the repository root:

```bash
# Restore local tools (dotnet-ef) and build
dotnet tool restore
dotnet build

# Run the API (Development environment)
dotnet run --project src/LinkAnalytics.Api
```

The API listens on `http://localhost:5087`. In Development:

- The SQLite database file is created and migrated automatically on startup.
- The OpenAPI document is available at `http://localhost:5087/openapi/v1.json`.
- Short URLs use `http://localhost:5087` as base address (set in `appsettings.Development.json`).

To use HTTPS (`https://localhost:7129`), trust the development certificate once and use the `https` launch profile:

```bash
dotnet dev-certs https --trust
dotnet run --project src/LinkAnalytics.Api --launch-profile https
```

Quick check:

```bash
curl http://localhost:5087/health
```

```json
{ "status": "Healthy", "timestamp": "2026-10-08T12:00:00.0000000+00:00" }
```

Request examples for every endpoint are in [`src/LinkAnalytics.Api/LinkAnalytics.Api.http`](src/LinkAnalytics.Api/LinkAnalytics.Api.http) (runnable from Visual Studio, VS Code with the REST Client extension, or JetBrains Rider).

## Configuration

| Setting                        | Environment variable              | Description                                                                                         | Default                                        |
| ------------------------------ | --------------------------------- | --------------------------------------------------------------------------------------------------- | ---------------------------------------------- |
| `ShortLinks:PublicBaseUrl`     | `ShortLinks__PublicBaseUrl`       | Public address used to build short URLs. Absolute HTTP/HTTPS URL, may include a path (`https://example.com/go`), no query string, fragment or credentials. **Required**: the app does not start if it is missing or invalid. | `http://localhost:5087` in Development, empty otherwise |
| `AllowedHosts`                 | `AllowedHosts`                    | Semicolon-separated host names the API answers to. Requests with any other `Host` header get `400`. | `localhost;127.0.0.1;[::1]`                    |
| `ConnectionStrings:LinkAnalytics` | `ConnectionStrings__LinkAnalytics` | SQLite connection string.                                                                        | `Data Source=linkanalytics.db`                 |

Outside Development you must set at least the public base URL and the allowed hosts, for example:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ShortLinks__PublicBaseUrl=https://links.example.com
export AllowedHosts=links.example.com
```

When an HTTPS endpoint is configured, HTTP requests are redirected to HTTPS. HSTS is enabled outside Development.

## Database and migrations

The API uses SQLite through Entity Framework Core. With the default connection string, the database file `linkanalytics.db` is created in the working directory, which is `src/LinkAnalytics.Api/` when using `dotnet run --project` or `dotnet ef`. Database files are excluded from Git.

- **Development:** pending migrations are applied automatically on startup.
- **Other environments:** apply migrations explicitly before starting the API:

  ```bash
  dotnet ef database update --project src/LinkAnalytics.Api
  ```

To create a new migration after changing the model:

```bash
dotnet ef migrations add <MigrationName> --project src/LinkAnalytics.Api --output-dir Data/Migrations
```

Schema:

- `ShortLinks`: `Id`, `OriginalUrl` (max. 2048), `Code` (unique index), `CreatedAt` (UTC)
- `LinkClicks`: `Id`, `ShortLinkId` (FK, cascade delete), `ClickedAt` (UTC), index on `(ShortLinkId, ClickedAt)`

## Endpoints

| Method | Route                     | Description                                          | Responses |
| ------ | ------------------------- | ---------------------------------------------------- | --------- |
| GET    | `/health`                 | Returns the API health status.                       | 200       |
| POST   | `/api/links`              | Creates a short link.                                | 201, 400  |
| GET    | `/api/links/{code}`       | Returns the details of a short link.                 | 200, 404  |
| GET    | `/api/links/{code}/stats` | Returns the access statistics of a short link.       | 200, 404  |
| GET    | `/r/{code}`               | Records an access and redirects to the original URL. | 302, 404  |

Codes are case-sensitive. All error responses use the `application/problem+json` format.

### Create a short link

```bash
curl -i -X POST http://localhost:5087/api/links \
  -H "Content-Type: application/json" \
  -d '{"url": "https://example.com/docs?page=1"}'
```

Response `201 Created`, with header `Location: /api/links/aZ3kP9x`:

```json
{
  "id": 1,
  "code": "aZ3kP9x",
  "originalUrl": "https://example.com/docs?page=1",
  "shortUrl": "http://localhost:5087/r/aZ3kP9x",
  "createdAt": "2026-10-08T12:00:00Z"
}
```

Validation rules:

- The URL is required and must be absolute, using `http` or `https`.
- Credentials in the URL (`https://user:pass@host`) are rejected.
- The maximum length is 2048 characters.
- The URL is normalized before being stored (e.g. `HTTP://Example.COM` becomes `http://example.com/`).
- Each request creates a new short link, even for a URL that was already shortened.

Invalid input returns `400 Bad Request`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.1",
  "title": "One or more validation errors occurred.",
  "status": 400,
  "errors": {
    "url": ["The URL must be an absolute HTTP or HTTPS address."]
  }
}
```

### Get a short link

```bash
curl http://localhost:5087/api/links/aZ3kP9x
```

Returns `200 OK` with the same body as above, or `404 Not Found`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404
}
```

### Redirect

```bash
curl -i http://localhost:5087/r/aZ3kP9x
```

```http
HTTP/1.1 302 Found
Cache-Control: no-store
Location: https://example.com/docs?page=1
```

Each request records one access and returns `302 Found`. An unknown code returns `404 Not Found` and records nothing. The temporary redirect and `Cache-Control: no-store` make sure browsers and proxies do not cache the response, so every access reaches the API and is counted.

### Access statistics

```bash
curl http://localhost:5087/api/links/aZ3kP9x/stats
```

Response `200 OK`:

```json
{
  "code": "aZ3kP9x",
  "totalClicks": 5,
  "lastClickAt": "2026-10-04T08:00:00Z",
  "clicksByDay": [
    { "date": "2026-10-01", "clicks": 3 },
    { "date": "2026-10-02", "clicks": 1 },
    { "date": "2026-10-04", "clicks": 1 }
  ]
}
```

- Days are calculated in UTC and sorted chronologically. Days without accesses are omitted.
- A link with no accesses returns `"totalClicks": 0`, `"lastClickAt": null` and `"clicksByDay": []`.
- An unknown code returns `404 Not Found`.
- Counting, grouping and the last-access lookup run in the database (`COUNT`, `GROUP BY`, `MAX`), so individual access records are never loaded into memory.

## Running tests

```bash
dotnet test
```

The suite contains unit tests and integration tests. Integration tests host the API in memory with `WebApplicationFactory`. Each test gets its own SQLite in-memory database, created with the real EF Core migrations, so tests are isolated, run in parallel safely and never touch the local database file. A controllable clock (`TimeProvider`) makes date-based tests deterministic.

## Known limitations

- **Every GET counts as an access.** Bots, crawlers and link previews in chat apps are counted too. Filtering them would require inspecting visitor data, which the API intentionally does not collect.
- **Statistics are grouped by UTC day.** An access at 22:00 in UTC−3 counts toward the next day.
- **The access is recorded before redirecting.** If the database write fails, the visitor gets a `500` error instead of being redirected.
- **No authentication or rate limiting.** Anyone who can reach the API can create links and read statistics. Do not expose it publicly as is.
- **No link management.** Links cannot be edited, deleted or set to expire.
- **SQLite is a single-file database.** It suits a single instance; running several instances would require a server database.
- **Reverse proxies are not configured.** When running behind a proxy or load balancer that terminates TLS, Forwarded Headers middleware would need to be configured for the HTTPS redirection to work correctly.
