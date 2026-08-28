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
    new ServiceDefinition("coupon-service-openapi.json", "Coupon Service", "coupon_base_url"),
    new ServiceDefinition("order-api-openapi.json", "Order API", "order_base_url"),
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
};

var sortKey = -1000;
foreach (var service in services)
{
    var openApiPath = Path.Combine(generatedDir, service.FileName);
    if (!File.Exists(openApiPath))
    {
        Console.WriteLine($"Skipping missing OpenAPI document: {openApiPath}");
        continue;
    }

    using var document = JsonDocument.Parse(File.ReadAllText(openApiPath));
    var folderId = StableId($"folder:{service.FolderName}");
    resources.Add(CreateRequestGroup(folderId, "wrk_coupon_service", service.FolderName,
        $"Generated from {service.FileName}. Re-import this workspace after dotnet build to refresh.",
        sortKey));
    sortKey += 100;

    var requestSortKey = sortKey;
    foreach (var request in InsomniaRequestFactory.CreateRequests(document, folderId, service.BaseUrlVariable))
    {
        request["metaSortKey"] = requestSortKey++;
        resources.Add(request);
    }
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
return 0;

static JsonObject CreateWorkspace() =>
    new()
    {
        ["_type"] = "workspace",
        ["_id"] = "wrk_coupon_service",
        ["name"] = "Coupon Service",
        ["description"] =
            "Auto-generated from OpenAPI on dotnet build.\n\n" +
            "Source specs: docs/api/generated/*-openapi.json\n" +
            "Environment: insomnia/environments/local.json\n\n" +
            "Do not hand-edit coupon-service.insomnia.json — run dotnet build or scripts/sync-insomnia-from-openapi.ps1.",
    };

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

internal sealed record ServiceDefinition(string FileName, string FolderName, string BaseUrlVariable);

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
