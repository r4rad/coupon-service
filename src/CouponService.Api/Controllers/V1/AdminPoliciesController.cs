using System.Text;
using System.Text.Json;
using CouponService.Api.Middleware;
using CouponService.Application.Policies;
using CouponService.Application.Redemption;
using CouponService.Engine.Ast;
using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;
using CouponService.Engine.Parsing;
using CouponService.Engine.Validation;
using Microsoft.AspNetCore.Mvc;

namespace CouponService.Api.Controllers.V1;

[ApiController]
[Route("v1/admin/policies")]
[Tags("Admin")]
public sealed class AdminPoliciesController(
    IPolicyRepository policies,
    IFactRegistry factRegistry) : ControllerBase
{
    private static readonly PolicyValidator Validator = new();

    [HttpGet]
    [EndpointSummary("List policies")]
    [EndpointDescription("Returns every stored policy document, including Archived ones.")]
    [ProducesResponseType(typeof(IReadOnlyList<AdminPolicyResponse>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<AdminPolicyResponse>>> ListAsync(
        CancellationToken cancellationToken)
    {
        var records = await policies.ListAsync(cancellationToken).ConfigureAwait(false);
        return Ok(records.Select(ToResponse).ToArray());
    }

    [HttpPost]
    [EndpointSummary("Create a policy")]
    [EndpointDescription(
        "Validates the document against the engine manifest, then persists it. Invalid documents never reach storage.")]
    [ProducesResponseType(typeof(AdminPolicyResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPolicyResponse>> CreateAsync(
        [FromBody] JsonElement document,
        CancellationToken cancellationToken)
    {
        var documentJson = document.GetRawText();
        var failure = TryValidateForWrite(documentJson, out var record);
        if (failure is not null)
        {
            return failure;
        }

        try
        {
            var created = await policies.CreateAsync(record!, cancellationToken).ConfigureAwait(false);
            var response = ToResponse(created);
            return Created($"/v1/admin/policies/{created.PolicyId}", response);
        }
        catch (InvalidOperationException ex)
        {
            return Conflict(CreateProblem(StatusCodes.Status409Conflict, "Conflict", ex.Message));
        }
    }

    [HttpGet("{id}")]
    [EndpointSummary("Get a policy by id")]
    [ProducesResponseType(typeof(AdminPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    public async Task<ActionResult<AdminPolicyResponse>> GetAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var record = await policies.GetByPolicyIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (record is null)
        {
            return NotFound();
        }

        return Ok(ToResponse(record));
    }

    [HttpPut("{id}")]
    [EndpointSummary("Update a policy")]
    [EndpointDescription(
        "Requires a matching If-Match ETag. Stale writes return 412. The document is re-validated against the manifest before replace.")]
    [ProducesResponseType(typeof(AdminPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status422UnprocessableEntity)]
    public async Task<ActionResult<AdminPolicyResponse>> UpdateAsync(
        string id,
        [FromBody] JsonElement document,
        CancellationToken cancellationToken)
    {
        if (!Request.Headers.TryGetValue("If-Match", out var ifMatchValues)
            || string.IsNullOrWhiteSpace(ifMatchValues.ToString()))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Missing If-Match",
                "PUT requires an If-Match ETag header."));
        }

        var existing = await policies.GetByPolicyIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound();
        }

        var documentJson = document.GetRawText();
        var failure = TryValidateForWrite(documentJson, out var record);
        if (failure is not null)
        {
            return failure;
        }

        if (!string.Equals(record!.PolicyId, id, StringComparison.Ordinal))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Policy id mismatch",
                "The document $.policyId must match the path id."));
        }

        if (!string.Equals(record.PartitionKey, existing.PartitionKey, StringComparison.Ordinal))
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Partition key immutable",
                "Code and trigger determine the partition key and cannot change on update."));
        }

        try
        {
            var replaced = await policies
                .ReplaceAsync(record, ifMatchValues.ToString(), cancellationToken)
                .ConfigureAwait(false);
            return Ok(ToResponse(replaced));
        }
        catch (PreconditionFailedException)
        {
            return StatusCode(
                StatusCodes.Status412PreconditionFailed,
                CreateProblem(
                    StatusCodes.Status412PreconditionFailed,
                    "Precondition Failed",
                    "The supplied ETag does not match the current policy version."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    [HttpDelete("{id}")]
    [EndpointSummary("Archive a policy")]
    [EndpointDescription(
        "Transitions the policy to Archived without removing the document, so historical orders stay explainable.")]
    [ProducesResponseType(typeof(AdminPolicyResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status404NotFound)]
    [ProducesResponseType(typeof(ProblemDetails), StatusCodes.Status412PreconditionFailed)]
    public async Task<ActionResult<AdminPolicyResponse>> DeleteAsync(
        string id,
        CancellationToken cancellationToken)
    {
        var existing = await policies.GetByPolicyIdAsync(id, cancellationToken).ConfigureAwait(false);
        if (existing is null)
        {
            return NotFound();
        }

        var archivedJson = WithStatus(existing.DocumentJson, "Archived");
        var archived = existing with { DocumentJson = archivedJson };

        try
        {
            var replaced = await policies
                .ReplaceAsync(archived, existing.ETag, cancellationToken)
                .ConfigureAwait(false);
            return Ok(ToResponse(replaced));
        }
        catch (PreconditionFailedException)
        {
            return StatusCode(
                StatusCodes.Status412PreconditionFailed,
                CreateProblem(
                    StatusCodes.Status412PreconditionFailed,
                    "Precondition Failed",
                    "The policy changed before it could be archived."));
        }
        catch (KeyNotFoundException)
        {
            return NotFound();
        }
    }

    private ActionResult? TryValidateForWrite(string documentJson, out PolicyRecord? record)
    {
        record = null;

        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(documentJson);
        }
        catch (JsonException ex)
        {
            return BadRequest(CreateProblem(
                StatusCodes.Status400BadRequest,
                "Invalid JSON",
                ex.Message));
        }

        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty("policyId", out var policyIdElement)
                || policyIdElement.ValueKind is not JsonValueKind.String
                || string.IsNullOrWhiteSpace(policyIdElement.GetString()))
            {
                return BadRequest(CreateNodeErrors(
                    StatusCodes.Status400BadRequest,
                    "Policy validation failed",
                    [new PolicyNodeError("$.policyId", "Policy document requires a non-empty $.policyId.")]));
            }

            if (!root.TryGetProperty("condition", out var condition))
            {
                return BadRequest(CreateNodeErrors(
                    StatusCodes.Status400BadRequest,
                    "Policy validation failed",
                    [new PolicyNodeError("$.condition", "Policy document requires $.condition.")]));
            }

            if (!root.TryGetProperty("effect", out _))
            {
                return BadRequest(CreateNodeErrors(
                    StatusCodes.Status400BadRequest,
                    "Policy validation failed",
                    [new PolicyNodeError("$.effect", "Policy document requires $.effect.")]));
            }

            var engineSchema = root.TryGetProperty("engineSchema", out var schemaElement)
                ? schemaElement.GetString() ?? string.Empty
                : string.Empty;

            Expr conditionExpr;
            try
            {
                var budget = new ParseBudget(
                    EngineLimits.Default.MaxParseNodes,
                    EngineLimits.Default.MaxParseDepth);
                conditionExpr = PolicyParser.Parse(condition.Clone(), budget, PolicyValidator.ConditionPath);
            }
            catch (PolicySyntaxException ex)
            {
                return BadRequest(CreateNodeErrors(
                    StatusCodes.Status400BadRequest,
                    "Policy syntax error",
                    [new PolicyNodeError(ex.Path, ex.Message)]));
            }
            catch (PolicyBudgetException ex)
            {
                return BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Policy budget exceeded",
                    ex.Message));
            }

            var validation = Validator.Validate(engineSchema, conditionExpr, factRegistry);
            if (!validation.IsValid)
            {
                var errors = validation.Errors
                    .Select(error => new PolicyNodeError(error.Path, error.Message))
                    .ToArray();

                var otherErrors = errors
                    .Where(error => !string.Equals(error.Path, "$.engineSchema", StringComparison.Ordinal))
                    .ToArray();

                if (otherErrors.Length > 0)
                {
                    // AC-2.3 / AC-6.1: unknown facts and type errors are HTTP 400 with every offending node.
                    return BadRequest(CreateNodeErrors(
                        StatusCodes.Status400BadRequest,
                        "Policy validation failed",
                        errors));
                }

                return UnprocessableEntity(CreateNodeErrors(
                    StatusCodes.Status422UnprocessableEntity,
                    "Unsupported engine schema",
                    errors));
            }

            try
            {
                record = PolicyRecordFactory.FromDocument(documentJson);
            }
            catch (Exception ex) when (ex is InvalidOperationException or JsonException or KeyNotFoundException)
            {
                return BadRequest(CreateProblem(
                    StatusCodes.Status400BadRequest,
                    "Invalid policy document",
                    ex.Message));
            }

            return null;
        }
    }

    private ProblemDetails CreateProblem(int status, string title, string detail)
    {
        var problem = new ProblemDetails
        {
            Status = status,
            Title = title,
            Detail = detail,
            Type = status switch
            {
                StatusCodes.Status412PreconditionFailed => "https://tools.ietf.org/html/rfc9110#section-15.5.13",
                StatusCodes.Status422UnprocessableEntity => "https://tools.ietf.org/html/rfc4918#section-11.2",
                StatusCodes.Status409Conflict => "https://tools.ietf.org/html/rfc9110#section-15.5.10",
                _ => "https://tools.ietf.org/html/rfc9110#section-15.5.1",
            },
        };

        var correlationId = HttpContext.Items[CorrelationIdMiddleware.ItemKey]?.ToString()
            ?? HttpContext.TraceIdentifier;
        problem.Extensions["correlationId"] = correlationId;
        return problem;
    }

    private ProblemDetails CreateNodeErrors(int status, string title, IReadOnlyList<PolicyNodeError> errors)
    {
        var problem = CreateProblem(status, title, "One or more policy nodes failed validation.");
        problem.Extensions["errors"] = errors;
        return problem;
    }

    private static AdminPolicyResponse ToResponse(PolicyRecord record)
    {
        using var document = JsonDocument.Parse(record.DocumentJson);
        return new AdminPolicyResponse(
            record.PolicyId,
            record.Code,
            record.Trigger,
            PolicyDocumentMetadata.ReadStatus(record.DocumentJson),
            record.ETag,
            document.RootElement.Clone());
    }

    // AC-6.4: soft-delete by status transition; the document must remain readable.
    private static string WithStatus(string documentJson, string status)
    {
        using var document = JsonDocument.Parse(documentJson);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            var wroteStatus = false;
            foreach (var property in document.RootElement.EnumerateObject())
            {
                if (property.NameEquals("status"))
                {
                    writer.WriteString("status", status);
                    wroteStatus = true;
                }
                else
                {
                    property.WriteTo(writer);
                }
            }

            if (!wroteStatus)
            {
                writer.WriteString("status", status);
            }

            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(stream.ToArray());
    }
}

public sealed record AdminPolicyResponse(
    string PolicyId,
    string? Code,
    PolicyTrigger Trigger,
    PolicyStatus Status,
    string ETag,
    JsonElement Document);

public sealed record PolicyNodeError(string Path, string Message);
