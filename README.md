# Link Analytics API

**English** | [Português (Brasil)](README.pt-BR.md)

A REST API built with ASP.NET Core that shortens URLs, redirects visitors to the original address and reports how often each short link is accessed.

> All data in this repository is fictional. URLs such as `https://example.com/docs` and domains such as `links.example.com` are reserved example addresses.

## Overview

Short links are easier to share than long URLs, and counting their accesses shows which links are actually used. A link shortener needs to do three things well: generate codes that are unique and hard to guess, redirect quickly, and record each access without collecting more data than necessary.

This project implements that core. It validates and stores URLs, generates random short codes, redirects `/r/{code}` to the original address while recording the access, and returns statistics per link: total accesses, last access and accesses per day. No IP address, location or other visitor data is stored.

## Quick Start

```bash
git clone https://github.com/dudapaula24/link-analytics-api.git
cd link-analytics-api
dotnet tool restore
dotnet run --project src/LinkAnalytics.Api
```

The API listens on `http://localhost:5087`. In the Development environment the SQLite database is created automatically, so no extra setup is needed.

```bash
curl http://localhost:5087/health
```

## Features

- **URL validation**: only absolute `http`/`https` URLs up to 2048 characters, without embedded credentials.
- **Random short codes**: 7-character Base62 codes from a cryptographically secure generator, not sequential and not guessable.
- **Guaranteed uniqueness**: a unique database index plus automatic retry when a generated code is already taken.
- **Temporary redirects**: `302 Found` with `Cache-Control: no-store`, so every access reaches the API and is counted.
- **Access statistics**: total accesses, last access and accesses per day, calculated in the database.
- **Privacy by design**: only the date and time of each access is stored.
- **Safe short URLs**: built from a configured public base URL, never from the request `Host` header.
- **Standard errors**: every error response uses [RFC 9457 Problem Details](https://www.rfc-editor.org/rfc/rfc9457).
- **OpenAPI document** available in Development.

## Tech Stack

| Tool | Use |
|---|---|
| .NET 10 (LTS) / ASP.NET Core Minimal APIs | Language, runtime and HTTP endpoints |
| Entity Framework Core 10 | Data access and migrations |
| SQLite | Database |
| Microsoft.AspNetCore.OpenApi | OpenAPI 3.1 document generation |
| xUnit + `WebApplicationFactory` | Unit and integration tests |

- **Requirements:** .NET SDK 10.0.301 or later (pinned in `global.json`).
- **Tested with:** .NET SDK 10.0.401, ASP.NET Core 10.0.12, Entity Framework Core 10.0.12 and xUnit 2.9.3.

## How It Works

```
POST /api/links ────────────► validate URL ─► generate unique code ─► save ShortLink ─► 201 Created

GET  /r/{code} ─────────────► find link ─► save LinkClick (UTC) ─► 302 Found

GET  /api/links/{code}/stats ► find link ─► COUNT / GROUP BY day / MAX in SQL ─► 200 OK
```

### Short codes

Each code has 7 characters from the Base62 alphabet (`0-9`, `A-Z`, `a-z`), which gives about 3.5 trillion combinations. Codes are case-sensitive. Before saving, the API checks whether the code already exists and generates another one if it does. If two requests still insert the same code at the same moment, the unique index rejects the second one and the API retries, up to 5 attempts.

### Access statistics

Every request to `/r/{code}` stores one access with its UTC timestamp, then redirects. The statistics endpoint counts accesses, groups them by UTC day and finds the most recent one using SQL aggregations (`COUNT`, `GROUP BY`, `MAX`), backed by an index on `(ShortLinkId, ClickedAt)`. Individual access records are never loaded into memory.

### Design decisions

- **`302` instead of `301`.** Browsers cache permanent redirects and stop calling the API, so accesses would be lost. `Cache-Control: no-store` also prevents proxies from storing the response.
- **Database as the final uniqueness guarantee.** Checking before inserting avoids most collisions, but only the unique index is safe against concurrent requests.
- **Configured public base URL.** The `Host` header is controlled by the client and can be spoofed, so short URLs are built from configuration. The API refuses to start if the value is missing or invalid.
- **UTC everywhere.** Dates are stored and returned in UTC, so results do not depend on the server's time zone.
- **Isolated test databases.** Each integration test runs against its own in-memory SQLite database created with the real migrations, so tests never share data and also validate the schema.

## Configuration

| Setting | Environment variable | Description | Default |
|---|---|---|---|
| `ShortLinks:PublicBaseUrl` | `ShortLinks__PublicBaseUrl` | Public address used to build short URLs. Absolute HTTP/HTTPS URL, may include a path (`https://example.com/go`), without query string, fragment or credentials. **Required.** | `http://localhost:5087` in Development, empty otherwise |
| `AllowedHosts` | `AllowedHosts` | Semicolon-separated host names the API answers to. Other `Host` headers receive `400`. | `localhost;127.0.0.1;[::1]` |
| `ConnectionStrings:LinkAnalytics` | `ConnectionStrings__LinkAnalytics` | SQLite connection string. | `Data Source=linkanalytics.db` |

Outside Development, set at least the public base URL and the allowed hosts:

```bash
export ASPNETCORE_ENVIRONMENT=Production
export ShortLinks__PublicBaseUrl=https://links.example.com
export AllowedHosts=links.example.com
```

When an HTTPS endpoint is configured, HTTP requests are redirected to HTTPS. HSTS is enabled outside Development. To run locally over HTTPS (`https://localhost:7129`):

```bash
dotnet dev-certs https --trust
dotnet run --project src/LinkAnalytics.Api --launch-profile https
```

## Database and Migrations

The database file `linkanalytics.db` is created in the working directory, which is `src/LinkAnalytics.Api/` when using `dotnet run --project` or `dotnet ef`. Database files are excluded from Git.

- **Development:** pending migrations are applied automatically on startup.
- **Other environments:** apply migrations before starting the API:

```bash
dotnet ef database update --project src/LinkAnalytics.Api
```

To create a new migration after changing the model:

```bash
dotnet ef migrations add <MigrationName> --project src/LinkAnalytics.Api --output-dir Data/Migrations
```

| Table | Columns |
|---|---|
| `ShortLinks` | `Id`, `OriginalUrl` (max. 2048), `Code` (unique), `CreatedAt` (UTC) |
| `LinkClicks` | `Id`, `ShortLinkId` (foreign key, cascade delete), `ClickedAt` (UTC) |

## API Endpoints

| Method | Route | Description | Responses |
|---|---|---|---|
| `GET` | `/health` | API health status | `200` |
| `POST` | `/api/links` | Creates a short link | `201`, `400` |
| `GET` | `/api/links/{code}` | Returns the details of a short link | `200`, `404` |
| `GET` | `/api/links/{code}/stats` | Returns the access statistics of a short link | `200`, `404` |
| `GET` | `/r/{code}` | Records an access and redirects to the original URL | `302`, `404` |

Error responses use the `application/problem+json` format. In Development, the OpenAPI document is available at `http://localhost:5087/openapi/v1.json`.

### URL rules

- Required, absolute, using `http` or `https`.
- Credentials in the URL (`https://user:pass@host`) are rejected.
- Maximum length of 2048 characters.
- Normalized before being stored: `HTTP://Example.COM` becomes `http://example.com/`.
- Each request creates a new short link, even for a URL that was already shortened.

## Example Requests

Ready-to-run requests for every endpoint are also available in [`src/LinkAnalytics.Api/LinkAnalytics.Api.http`](src/LinkAnalytics.Api/LinkAnalytics.Api.http).

**Create a short link**

```bash
curl -i -X POST http://localhost:5087/api/links \
  -H "Content-Type: application/json" \
  -d '{"url": "https://example.com/docs?page=1"}'
```

```http
HTTP/1.1 201 Created
Location: /api/links/aZ3kP9x
```

```json
{
  "id": 1,
  "code": "aZ3kP9x",
  "originalUrl": "https://example.com/docs?page=1",
  "shortUrl": "http://localhost:5087/r/aZ3kP9x",
  "createdAt": "2026-10-08T12:00:00Z"
}
```

**Invalid URL**

```bash
curl -X POST http://localhost:5087/api/links \
  -H "Content-Type: application/json" \
  -d '{"url": "ftp://example.com/file.txt"}'
```

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

**Get a short link**

```bash
curl http://localhost:5087/api/links/aZ3kP9x
```

Returns `200 OK` with the same body as the creation response, or `404 Not Found`:

```json
{
  "type": "https://tools.ietf.org/html/rfc9110#section-15.5.5",
  "title": "Not Found",
  "status": 404
}
```

**Redirect**

```bash
curl -i http://localhost:5087/r/aZ3kP9x
```

```http
HTTP/1.1 302 Found
Cache-Control: no-store
Location: https://example.com/docs?page=1
```

An unknown code returns `404 Not Found` and records nothing.

**Access statistics**

```bash
curl http://localhost:5087/api/links/aZ3kP9x/stats
```

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

Days are in UTC and sorted chronologically; days without accesses are omitted. A link without accesses returns `"totalClicks": 0`, `"lastClickAt": null` and `"clicksByDay": []`.

## Running Tests

```bash
dotnet test
```

The tests cover:

- **Link creation:** valid and invalid URLs, normalization, credentials, maximum length, malformed or empty bodies, unique codes and retry after a code collision.
- **Lookup and redirect:** existing and unknown codes, case-sensitive codes, `302` responses and `Cache-Control: no-store`.
- **Statistics:** recording accesses, multiple accesses, grouping by UTC day, links without accesses, unknown codes and isolation between links.
- **Configuration:** short URLs built from the public base URL, startup failure with invalid values, rejected `Host` headers and HTTPS redirection.
- **Code generator and health check.**

Integration tests host the API in memory with `WebApplicationFactory`. Each test gets its own SQLite in-memory database, and a controllable clock (`TimeProvider`) makes date-based tests deterministic.

## Project Structure

```text
link-analytics-api/
├── src/
│   └── LinkAnalytics.Api/
│       ├── Contracts/                # request and response models
│       ├── Data/                     # DbContext and EF Core migrations
│       ├── Endpoints/                # endpoint definitions grouped by feature
│       ├── Models/                   # entities (ShortLink, LinkClick)
│       ├── Services/                 # link creation, statistics, code generation, validation
│       ├── appsettings*.json         # configuration
│       ├── LinkAnalytics.Api.http    # request examples
│       └── Program.cs                # service registration and HTTP pipeline
├── tests/
│   └── LinkAnalytics.Tests/
│       ├── Endpoints/                # integration tests
│       ├── Infrastructure/           # test host with an isolated database
│       └── Services/                 # unit tests
├── dotnet-tools.json                 # local tools (dotnet-ef)
├── global.json                       # .NET SDK version
└── LinkAnalytics.slnx                # solution file
```

## Limitations

- Every `GET` to `/r/{code}` counts as an access, including bots, crawlers and link previews in chat apps. Filtering them would require inspecting visitor data, which the API intentionally does not collect.
- Statistics are grouped by UTC day: an access at 22:00 in UTC−3 counts toward the next day.
- The access is recorded before redirecting. If the database write fails, the visitor receives a `500` error instead of being redirected.
- There is no authentication or rate limiting. Anyone who can reach the API can create links and read statistics, so it should not be exposed publicly as is.
- Links cannot be edited, deleted or set to expire.
- SQLite suits a single instance; running several instances would require a server database.
- Forwarded headers are not configured. Behind a proxy or load balancer that terminates TLS, HTTPS redirection would need additional setup.

## Future Improvements

- Authentication and rate limiting.
- Link expiration and deletion.
- Statistics in a time zone chosen by the client.
- A server database such as PostgreSQL for multiple instances.
- Docker image.

## Author

Developed by [dudapaula24](https://github.com/dudapaula24).
