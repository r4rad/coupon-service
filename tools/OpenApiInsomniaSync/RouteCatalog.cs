using System.Text.Json;
using System.Text.Json.Nodes;

internal static class RouteCatalogLoader
{
    internal static RouteCatalog Load(string path)
    {
        if (!File.Exists(path))
        {
            return new RouteCatalog([]);
        }

        var json = File.ReadAllText(path);
        var document = JsonNode.Parse(json)!.AsObject();
        var folders = new List<RouteFolder>();

        if (document.TryGetPropertyValue("folders", out var foldersNode) && foldersNode is JsonArray foldersArray)
        {
            foreach (var folderNode in foldersArray)
            {
                if (folderNode is not JsonObject folderObject)
                {
                    continue;
                }

                var routes = new List<RouteDefinition>();
                if (folderObject.TryGetPropertyValue("routes", out var routesNode) && routesNode is JsonArray routesArray)
                {
                    foreach (var routeNode in routesArray)
                    {
                        if (routeNode is not JsonObject routeObject)
                        {
                            continue;
                        }

                        routes.Add(new RouteDefinition(
                            routeObject["name"]?.GetValue<string>() ?? "Unnamed route",
                            routeObject["method"]?.GetValue<string>() ?? "GET",
                            routeObject["path"]?.GetValue<string>() ?? "/",
                            routeObject["description"]?.GetValue<string>(),
                            routeObject["auth"]?.GetValue<string>() ?? "none",
                            routeObject["body"],
                            ReadHeaders(routeObject)));
                    }
                }

                folders.Add(new RouteFolder(
                    folderObject["name"]?.GetValue<string>() ?? "Routes",
                    folderObject["description"]?.GetValue<string>(),
                    routes));
            }
        }

        return new RouteCatalog(folders);
    }

    internal static string MergeIntoOpenApi(string openApiText, RouteCatalog catalog)
    {
        var root = JsonNode.Parse(openApiText)!.AsObject();
        var paths = root["paths"] as JsonObject ?? new JsonObject();
        root["paths"] = paths;

        foreach (var folder in catalog.Folders)
        {
            foreach (var route in folder.Routes)
            {
                var pathKey = NormalizeOpenApiPath(route.Path);
                var pathItem = paths[pathKey] as JsonObject ?? new JsonObject();
                paths[pathKey] = pathItem;

                var method = route.Method.ToLowerInvariant();
                if (pathItem.ContainsKey(method))
                {
                    continue;
                }

                var operation = new JsonObject
                {
                    ["summary"] = route.Name,
                    ["operationId"] = CreateOperationId(route.Method, pathKey),
                };

                if (!string.IsNullOrWhiteSpace(route.Description))
                {
                    operation["description"] = route.Description;
                }

                if (route.Body is not null)
                {
                    operation["requestBody"] = new JsonObject
                    {
                        ["required"] = true,
                        ["content"] = new JsonObject
                        {
                            ["application/json"] = new JsonObject
                            {
                                ["example"] = route.Body.DeepClone(),
                            },
                        },
                    };
                }

                if (route.Auth is not "none")
                {
                    operation["security"] = new JsonArray
                    {
                        new JsonObject
                        {
                            ["bearerAuth"] = new JsonArray(),
                        },
                    };
                }

                pathItem[method] = operation;
            }
        }

        if (root["components"] is not JsonObject components)
        {
            components = new JsonObject();
            root["components"] = components;
        }

        if (components["securitySchemes"] is not JsonObject securitySchemes)
        {
            securitySchemes = new JsonObject();
            components["securitySchemes"] = securitySchemes;
        }

        securitySchemes["bearerAuth"] = new JsonObject
        {
            ["type"] = "http",
            ["scheme"] = "bearer",
            ["bearerFormat"] = "JWT",
        };

        return root.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
    }

    private static IReadOnlyList<RouteHeader> ReadHeaders(JsonObject routeObject)
    {
        if (routeObject["headers"] is not JsonArray headersArray)
        {
            return [];
        }

        var headers = new List<RouteHeader>();
        foreach (var headerNode in headersArray)
        {
            if (headerNode is not JsonObject headerObject)
            {
                continue;
            }

            var name = headerObject["name"]?.GetValue<string>();
            var value = headerObject["value"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(name) || value is null)
            {
                continue;
            }

            headers.Add(new RouteHeader(name, value));
        }

        return headers;
    }

    private static string NormalizeOpenApiPath(string path)
    {
        if (path.StartsWith("/v1/reservations/", StringComparison.Ordinal)
            && path.EndsWith("/confirm", StringComparison.Ordinal))
        {
            return "/v1/reservations/{orderId}/confirm";
        }

        if (path.StartsWith("/v1/reservations/", StringComparison.Ordinal)
            && path.EndsWith("/release", StringComparison.Ordinal))
        {
            return "/v1/reservations/{orderId}/release";
        }

        if (path.StartsWith("/v1/admin/policies/", StringComparison.Ordinal)
            && !path.EndsWith("/simulate", StringComparison.Ordinal)
            && !path.EndsWith("/status", StringComparison.Ordinal)
            && path != "/v1/admin/policies")
        {
            return "/v1/admin/policies/{policyId}";
        }

        if (path.EndsWith("/simulate", StringComparison.Ordinal))
        {
            return "/v1/admin/policies/{policyId}/simulate";
        }

        if (path.EndsWith("/status", StringComparison.Ordinal))
        {
            return "/v1/admin/policies/{policyId}/status";
        }

        if (path.StartsWith("/v1/orders/", StringComparison.Ordinal) && path != "/v1/orders")
        {
            return "/v1/orders/{orderId}";
        }

        return path;
    }

    private static string CreateOperationId(string method, string path) =>
        $"{method.ToLowerInvariant()}_{path.Trim('/').Replace('/', '_').Replace("{", "").Replace("}", "")}";
}

internal sealed record RouteCatalog(IReadOnlyList<RouteFolder> Folders);

internal sealed record RouteFolder(string Name, string? Description, IReadOnlyList<RouteDefinition> Routes);

internal sealed record RouteDefinition(
    string Name,
    string Method,
    string Path,
    string? Description,
    string Auth,
    JsonNode? Body,
    IReadOnlyList<RouteHeader> Headers);

internal sealed record RouteHeader(string Name, string Value);

internal static class RouteCatalogRequestFactory
{
    internal static (List<JsonObject> Requests, int NextSortKey) CreateRequests(
        RouteCatalog catalog,
        string serviceParentId,
        string baseUrlVariable,
        int sortKey)
    {
        var results = new List<JsonObject>();
        foreach (var folder in catalog.Folders)
        {
            var folderId = StableId($"route-folder:{serviceParentId}:{folder.Name}");
            results.Add(CreateRequestGroup(folderId, serviceParentId, folder.Name, folder.Description ?? string.Empty, sortKey));
            sortKey += 10;

            foreach (var route in folder.Routes)
            {
                results.Add(CreateRequest(route, folderId, baseUrlVariable, sortKey++));
            }
        }

        return (results, sortKey);
    }

    private static JsonObject CreateRequest(
        RouteDefinition route,
        string parentFolderId,
        string baseUrlVariable,
        int metaSortKey)
    {
        var headers = CreateHeaders(route);
        var request = new JsonObject
        {
            ["_type"] = "request",
            ["_id"] = $"req_{StableId($"{parentFolderId}:{route.Method}:{route.Path}:{route.Name}")}",
            ["parentId"] = parentFolderId,
            ["name"] = route.Name,
            ["method"] = route.Method.ToUpperInvariant(),
            ["url"] = $"{{{{ _.{baseUrlVariable} }}}}{route.Path}",
            ["headers"] = headers,
            ["authentication"] = new JsonObject(),
            ["metaSortKey"] = metaSortKey,
        };

        if (!string.IsNullOrWhiteSpace(route.Description))
        {
            request["description"] = route.Description;
        }

        if (route.Body is not null)
        {
            request["body"] = new JsonObject
            {
                ["mimeType"] = "application/json",
                ["text"] = route.Body.ToJsonString(new JsonSerializerOptions { WriteIndented = true }),
            };
        }

        return request;
    }

    private static JsonArray CreateHeaders(RouteDefinition route)
    {
        var headers = new JsonArray
        {
            new JsonObject
            {
                ["name"] = "Accept",
                ["value"] = "application/json",
            },
        };

        if (route.Body is not null)
        {
            headers.Add(new JsonObject
            {
                ["name"] = "Content-Type",
                ["value"] = "application/json",
            });
        }

        foreach (var header in route.Headers)
        {
            headers.Add(new JsonObject
            {
                ["name"] = header.Name,
                ["value"] = header.Value,
            });
        }

        headers.Add(new JsonObject
        {
            ["name"] = "X-Correlation-Id",
            ["value"] = "{{ _.correlation_id }}",
        });

        if (route.Auth is not "none")
        {
            var tokenVariable = route.Auth switch
            {
                "admin" => "admin_token",
                "redeem" => "redeem_token",
                _ => "customer_token",
            };

            headers.Add(new JsonObject
            {
                ["name"] = "Authorization",
                ["value"] = $"Bearer {{{{ _.{tokenVariable} }}}}",
            });
        }

        return headers;
    }

    private static JsonObject CreateRequestGroup(
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

    private static string StableId(string input)
    {
        var hash = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(hash)[..12].ToLowerInvariant();
    }
}
