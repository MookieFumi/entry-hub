# API versioning: route groups or Asp.Versioning

The same two versions of `GET /rooms` built twice, to compare what each approach gives you.
Neither project touches `EntryHub.Api`, which uses plain route groups under `/api/v1`.

| | `MapGroupVersioning` | `AspVersioning` |
|---|---|---|
| Packages | none | `Asp.Versioning.Http` |
| Version in the URL | `MapGroup("/api/v1")` | `MapGroup("/api/v{version:apiVersion}")` |
| Which version is deprecated | only in the OpenAPI document, with an operation transformer (see below) | `HasDeprecatedApiVersion`, also in the responses |
| Headers `api-supported-versions` and `api-deprecated-versions` | you write them | `ReportApiVersions = true` |
| Other ways to read the version (header, query string) | not available | `ApiVersionReader` |
| One OpenAPI document per version | one `AddOpenApi("v1")`, `AddOpenApi("v2")` and a route filter per document | the same, plus `Asp.Versioning.Mvc.ApiExplorer` to tell the documents which endpoints belong to which version |
| Unknown version (`/api/v3/rooms`) | 404 | 404 |

## One OpenAPI document per version

`MapGroupVersioning` also publishes one document per version. Each `AddOpenApi("v1")` and `AddOpenApi("v2")` keeps
only the endpoints under its own prefix (`ShouldInclude`), and an operation transformer marks every operation of
version 1 as deprecated. Scalar shows both documents with a selector, version 2 first.

The build writes both files to `MapGroupVersioning/openapi/`: `MapGroupVersioning.json` for the first document
(`v1`) and `MapGroupVersioning_v2.json` for the second. They are committed so the difference between the two
versions can be read without running anything.

```bash
curl http://localhost:5301/openapi/v1.json
curl http://localhost:5301/openapi/v2.json
# Scalar: http://localhost:5301/scalar/
```

## Run them

```bash
cd src/samples/versioning
dotnet run --project MapGroupVersioning --urls http://localhost:5301
dotnet run --project AspVersioning --urls http://localhost:5302
curl -i http://localhost:5302/api/v1/rooms
```

The second project answers version 1 with `api-deprecated-versions: 1.0`; the first answers it with nothing extra.

## When to pick which

Route groups are enough while versions only live in the URL, the team controls every client and a deprecation is a
message in a changelog. Asp.Versioning pays off when you need to announce deprecations in the responses, accept the
version from a header or the query string, or keep many versions documented side by side.
