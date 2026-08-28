using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

var repoRoot = args.Length > 0
    ? Path.GetFullPath(args[0])
    : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));

var generatedDir = Path.Combine(repoRoot, "docs", "api", "generated");
var environmentFile = Path.Combine(repoRoot, "insomnia", "environments", "local.json");
var outputFile = Path.Combine(repoRoot, "insomnia", "coupon-service.insomnia.json");

var services = new[]
{
    new ServiceDefinition(
        "coupon-service-openapi.json",
        "coupon-service.routes.json",
        "Coupon Service",
        "coupon_base_url",
        "coupon-service-design.insomnia.json"),
    new ServiceDefinition(
        "order-api-openapi.json",
        "order-api.routes.json",
        "Order API",
        "order_base_url",
        "order-api-design.insomnia.json"),
};

if (!Directory.Exists(generatedDir))
{
    Console.Error.WriteLine($"OpenAPI output directory not found: {generatedDir}");
    Console.Error.WriteLine("Build an API project first (OpenApiGenerateDocumentsOnBuild).");
    return 1;
}

var environment = LoadEnvironment(environmentFile);
var resources = new JsonArray
{
    CreateWorkspace(),
    CreateEnvironment("env_base", "wrk_coupon_service", null, "Base Environment", new JsonObject(), null, null),
    environment,
    CreateRequestGroup(
        "fld_documentation",
        "wrk_coupon_service",
        "Documentation (Swagger / Redoc preview)",
        "Open these design specs in Insomnia for a rendered OpenAPI preview.\n\n" +
        "Import each *-design.insomnia.json file as a Design Document, then use the Design tab.\n" +
        "From the design document: Settings → Generate collection → Debug tab to send requests.",
        -1100),
};

var designExports = new List<string>();
var sortKey = -1000;
foreach (var service in services)
{
    var openApiPath = Path.Combine(generatedDir, service.FileName);
    var openApiText = File.Exists(openApiPath)
        ? File.ReadAllText(openApiPath)
        : """{"openapi":"3.1.1","info":{"title":"API","version":"v1"},"paths":{}}""";

    var routesPath = Path.Combine(repoRoot, "insomnia", "routes", service.RoutesCatalogFileName);
    var routeCatalog = RouteCatalogLoader.Load(routesPath);
    var mergedOpenApiText = RouteCatalogLoader.MergeIntoOpenApi(openApiText, routeCatalog);

    var specId = StableId($"spec:{service.FileName}");
    resources.Add(CreateApiSpec(specId, "fld_documentation", service.FileName, mergedOpenApiText));

    var designOutput = Path.Combine(repoRoot, "insomnia", service.DesignExportFileName);
    WriteDesignDocumentExport(designOutput, service, mergedOpenApiText, environment);
    designExports.Add(designOutput);

    var serviceFolderId = StableId($"folder:{service.FolderName}");
    resources.Add(CreateRequestGroup(
        serviceFolderId,
        "wrk_coupon_service",
        service.FolderName,
        $"Dedicated /v1 routes for {service.FolderName}. Edit insomnia/routes/{service.RoutesCatalogFileName} to add routes.",
        sortKey));
    sortKey += 100;

    var folderSortKey = sortKey;
    var (routeRequests, nextSortKey) = RouteCatalogRequestFactory.CreateRequests(
        routeCatalog,
        serviceFolderId,
        service.BaseUrlVariable,
        folderSortKey);
    foreach (var request in routeRequests)
    {
        resources.Add(request);
    }

    sortKey = nextSortKey;
}

var export = new JsonObject
{
    ["_type"] = "export",
    ["__export_format"] = 4,
    ["__export_date"] = DateTime.UtcNow.ToString("o"),
    ["__export_source"] = "OpenApiInsomniaSync",
    ["resources"] = resources,
};

Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
File.WriteAllText(outputFile, export.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));

Console.WriteLine($"Wrote Insomnia workspace: {outputFile}");
foreach (var designExport in designExports)
{
    Console.WriteLine($"Wrote Insomnia design document: {designExport}");
}
return 0;

static JsonObject CreateWorkspace() =>
    new()
    {
        ["_type"] = "workspace",
        ["_id"] = "wrk_coupon_service",
        ["name"] = "Coupon Service",
        ["description"] =
            "Auto-generated from OpenAPI on dotnet build.\n\n" +
            "Swagger / Redoc-style browsing: import insomnia/*-design.insomnia.json as a Design Document.\n" +
            "Try-it-out requests: use the Coupon Service / Order API folders below.\n\n" +
            "Regenerate: dotnet build or scripts/sync-insomnia-from-openapi.ps1",
    };

static JsonObject CreateApiSpec(string id, string parentId, string fileName, string contents) =>
    new()
    {
        ["_type"] = "api_spec",
        ["_id"] = id,
        ["parentId"] = parentId,
        ["fileName"] = fileName,
        ["contentType"] = "json",
        ["contents"] = contents,
    };

static void WriteDesignDocumentExport(
    string outputFile,
    ServiceDefinition service,
    string openApiText,
    JsonObject localEnvironment)
{
    var workspaceId = StableId($"design-wrk:{service.FileName}");
    var specId = StableId($"design-spec:{service.FileName}");
    var envBaseId = StableId($"design-env-base:{service.FileName}");
    var envLocalId = StableId($"design-env-local:{service.FileName}");

    var envLocal = localEnvironment.DeepClone().AsObject();
    envLocal["_id"] = envLocalId;
    envLocal["parentId"] = envBaseId;

    var resources = new JsonArray
    {
        new JsonObject
        {
            ["_type"] = "workspace",
            ["_id"] = workspaceId,
            ["name"] = $"{service.FolderName} (Design)",
            ["description"] =
                "Design document for Swagger / Redoc-style OpenAPI preview in Insomnia.\n\n" +
                "1. Open this document\n" +
                "2. Use the Design tab for the rendered spec preview\n" +
                "3. Settings → Generate collection, then Debug tab to send requests\n\n" +
                $"Source: docs/api/generated/{service.FileName}",
        },
        CreateEnvironment(envBaseId, workspaceId, null, "Base Environment", new JsonObject(), null, null),
        envLocal,
        CreateApiSpec(specId, workspaceId, service.FileName, openApiText),
    };

    var export = new JsonObject
    {
        ["_type"] = "export",
        ["__export_format"] = 4,
        ["__export_date"] = DateTime.UtcNow.ToString("o"),
        ["__export_source"] = "OpenApiInsomniaSync:design",
        ["resources"] = resources,
    };

    Directory.CreateDirectory(Path.GetDirectoryName(outputFile)!);
    File.WriteAllText(outputFile, export.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
}

static JsonObject LoadEnvironment(string path)
{
    if (File.Exists(path))
    {
        return JsonNode.Parse(File.ReadAllText(path))!.AsObject();
    }

    return CreateEnvironment(
        "env_local",
        "env_base",
        "#7d69cb",
        "Local",
        new JsonObject
        {
            ["coupon_base_url"] = "http://localhost:5174",
            ["order_base_url"] = "http://localhost:5043",
            ["customer_token"] = "",
            ["admin_token"] = "",
            ["redeem_token"] = "",
            ["correlation_id"] = "insomnia-local-001",
        },
        new JsonObject
        {
            ["&"] = new JsonArray
            {
                "coupon_base_url",
                "order_base_url",
                "customer_token",
                "admin_token",
                "redeem_token",
                "correlation_id",
            },
        },
        false);
}

static JsonObject CreateEnvironment(
    string id,
    string parentId,
    string? color,
    string name,
    JsonObject data,
    JsonObject? dataPropertyOrder,
    bool? isPrivate)
{
    var environment = new JsonObject
    {
        ["_type"] = "environment",
        ["_id"] = id,
        ["parentId"] = parentId,
        ["name"] = name,
        ["data"] = data,
        ["color"] = color,
        ["isPrivate"] = isPrivate ?? false,
    };

    if (dataPropertyOrder is not null)
    {
        environment["dataPropertyOrder"] = dataPropertyOrder;
    }

    return environment;
}

static JsonObject CreateRequestGroup(
    string id,
    string parentId,
    string name,
    string description,
    int metaSortKey) =>
    new()
    {
        ["_type"] = "request_group",
        ["_id"] = id,
        ["parentId"] = parentId,
        ["name"] = name,
        ["description"] = description,
        ["environment"] = new JsonObject(),
        ["metaSortKey"] = metaSortKey,
    };

static string StableId(string input)
{
    var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
    return Convert.ToHexString(hash)[..12].ToLowerInvariant();
}

internal sealed record ServiceDefinition(
    string FileName,
    string RoutesCatalogFileName,
    string FolderName,
    string BaseUrlVariable,
    string DesignExportFileName);

internal static class InsomniaRequestFactory
{
    private static readonly string[] HttpMethods =
        ["get", "post", "put", "patch", "delete", "head", "options"];

    internal static IEnumerable<JsonObject> CreateRequests(
        JsonDocument openApi,
        string parentFolderId,
        string baseUrlVariable)
    {
        if (!openApi.RootElement.TryGetProperty("paths", out var paths))
        {
            yield break;
        }

        foreach (var pathProperty in paths.EnumerateObject())
        {
            foreach (var operationProperty in pathProperty.Value.EnumerateObject())
            {
                if (!HttpMethods.Contains(operationProperty.Name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                yield return CreateRequest(
                    pathProperty.Name,
                    operationProperty.Name,
                    operationProperty.Value,
                    parentFolderId,
                    baseUrlVariable);
            }
        }
    }

    private static JsonObject CreateRequest(
        string path,
        string method,
        JsonElement operation,
        string parentFolderId,
        string baseUrlVariable)
    {
        var name = operation.TryGetProperty("summary", out var summary)
            ? summary.GetString()
            : null;

        name ??= operation.TryGetProperty("operationId", out var operationId)
            ? operationId.GetString()
            : null;

        name ??= $"{method.ToUpperInvariant()} {path}";

        var description = operation.TryGetProperty("description", out var descriptionElement)
            ? descriptionElement.GetString()
            : null;

        var url = BuildUrl(path, baseUrlVariable, operation);
        var headers = CreateHeaders(operation);
        var body = CreateBody(operation);

        var request = new JsonObject
        {
            ["_type"] = "request",
            ["_id"] = $"req_{StableId($"{parentFolderId}:{method}:{path}")}",
            ["parentId"] = parentFolderId,
            ["name"] = name,
            ["method"] = method.ToUpperInvariant(),
            ["url"] = url,
            ["headers"] = headers,
            ["authentication"] = new JsonObject(),
        };

        if (!string.IsNullOrWhiteSpace(description))
        {
            request["description"] = description;
        }

        if (body is not null)
        {
            request["body"] = body;
        }

        return request;
    }

    private static string BuildUrl(string path, string baseUrlVariable, JsonElement operation)
    {
        var resolvedPath = path;
        if (operation.TryGetProperty("parameters", out var parameters))
        {
            foreach (var parameter in parameters.EnumerateArray())
            {
                if (!parameter.TryGetProperty("in", out var location) || location.GetString() != "path")
                {
                    continue;
                }

                if (!parameter.TryGetProperty("name", out var nameElement))
                {
                    continue;
                }

                var parameterName = nameElement.GetString() ?? "value";
                var example = parameter.TryGetProperty("example", out var exampleElement)
                    ? exampleElement.ToString()
                    : parameterName;

                resolvedPath = resolvedPath.Replace(
                    $"{{{parameterName}}}",
                    example,
                    StringComparison.Ordinal);
            }
        }

        return $"{{{{ _.{baseUrlVariable} }}}}{resolvedPath}";
    }

    private static JsonArray CreateHeaders(JsonElement operation)
    {
        var headers = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "Accept",
                ["value"] = "application/json",
            },
        };

        if (operation.TryGetProperty("requestBody", out _))
        {
            headers.Add(new JsonObject
            {
                ["name"] = "Content-Type",
                ["value"] = "application/json",
            });
        }

        if (operation.TryGetProperty("security", out var security) && security.GetArrayLength() > 0)
        {
            headers.Add(new JsonObject
            {
                ["name"] = "Authorization",
                ["value"] = "Bearer {{ _.customer_token }}",
            });
        }

        headers.Add(new JsonObject
        {
            ["name"] = "X-Correlation-Id",
            ["value"] = "{{ _.correlation_id }}",
        });

        return headers;
    }

    private static JsonObject? CreateBody(JsonElement operation)
    {
        if (!operation.TryGetProperty("requestBody", out var requestBody))
        {
            return null;
        }

        if (!requestBody.TryGetProperty("content", out var content))
        {
            return null;
        }

        if (!content.TryGetProperty("application/json", out var jsonContent))
        {
            return null;
        }

        var example = ExtractExample(jsonContent);
        return new JsonObject
        {
            ["mimeType"] = "application/json",
            ["text"] = example,
        };
    }

    private static string ExtractExample(JsonElement jsonContent)
    {
        if (jsonContent.TryGetProperty("example", out var example))
        {
            return JsonSerializer.Serialize(example, new JsonSerializerOptions { WriteIndented = true });
        }

        if (jsonContent.TryGetProperty("examples", out var examples))
        {
            foreach (var candidate in examples.EnumerateObject())
            {
                if (candidate.Value.TryGetProperty("value", out var value))
                {
                    return JsonSerializer.Serialize(value, new JsonSerializerOptions { WriteIndented = true });
                }
            }
        }

        if (jsonContent.TryGetProperty("schema", out var schema))
        {
            var generated = ExampleGenerator.FromSchema(schema);
            if (generated is not null)
            {
                return JsonSerializer.Serialize(generated, new JsonSerializerOptions { WriteIndented = true });
            }
        }

        return "{}";
    }

    private static string StableId(string input)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }
}

internal static class ExampleGenerator
{
    internal static JsonNode? FromSchema(JsonElement schema)
    {
        if (schema.TryGetProperty("$ref", out _))
        {
            return new JsonObject();
        }

        if (schema.TryGetProperty("example", out var example))
        {
            return JsonNode.Parse(example.GetRawText());
        }

        if (schema.TryGetProperty("properties", out var properties))
        {
            var obj = new JsonObject();
            foreach (var property in properties.EnumerateObject())
            {
                if (property.Value.TryGetProperty("example", out var propertyExample))
                {
                    obj[property.Name] = JsonNode.Parse(propertyExample.GetRawText());
                    continue;
                }

                obj[property.Name] = DefaultForType(property.Value) ?? "";
            }

            return obj;
        }

        return DefaultForType(schema);
    }

    private static JsonNode? DefaultForType(JsonElement schema)
    {
        if (schema.TryGetProperty("type", out var typeElement))
        {
            return typeElement.GetString() switch
            {
                "string" => schema.TryGetProperty("format", out var format) && format.GetString() == "uuid"
                    ? "00000000-0000-0000-0000-000000000001"
                    : "string",
                "integer" => 1,
                "number" => 1.0,
                "boolean" => false,
                "array" => new JsonArray(),
                "object" => new JsonObject(),
                _ => null,
            };
        }

        return null;
    }
}
