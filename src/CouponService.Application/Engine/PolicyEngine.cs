using System.Text.Json;
using CouponService.Application.Policies;
using CouponService.Application.Pricing;
using CouponService.Application.Redemption;
using CouponService.Application.Validation;
using CouponService.Domain;
using CouponService.Engine.Ast;
using CouponService.Engine.Caching;
using CouponService.Engine.Compilation;
using CouponService.Engine.Effects;
using CouponService.Engine.Evaluation;
using CouponService.Engine.Facts;
using CouponService.Engine.Manifest;
using CouponService.Engine.Parsing;
using CouponService.Engine.Validation;

namespace CouponService.Application.Engine;

public sealed class PolicyEngine : IPolicyEngine
{
    private readonly IClock _clock;
    private readonly IFactRegistry _registry;
    private readonly PolicyCompiler _compiler;
    private readonly CompiledPolicyCache _cache;
    private readonly EffectApplier _effectApplier;
    private readonly IRedemptionRepository _redemptions;

    public PolicyEngine(
        IClock clock,
        IFactRegistry registry,
        IRedemptionRepository redemptions,
        CompiledPolicyCache? cache = null,
        PolicyCompiler? compiler = null,
        EffectApplier? effectApplier = null)
    {
        _clock = clock;
        _registry = registry;
        _redemptions = redemptions;
        _compiler = compiler ?? new PolicyCompiler();
        _cache = cache ?? new CompiledPolicyCache(clock);
        _effectApplier = effectApplier ?? EffectEngine.CreateStandardApplier();
    }

    public async Task<PolicyDecision> EvaluateAsync(
        PolicyRecord policy,
        Cart cart,
        CustomerContext customer,
        bool captureFullTrace = false,
        CancellationToken cancellationToken = default)
    {
        var contentHash = PolicyContentHasher.ComputeHash(policy.DocumentJson);
        var root = PolicyDocumentReader.ParseRoot(policy.DocumentJson);

        var engineSchema = PolicyDocumentReader.GetEngineSchema(root);
        if (!string.Equals(engineSchema, EngineManifestGenerator.CurrentEngineSchema, StringComparison.Ordinal))
        {
            return PolicyDecision.Rejected(RejectionReason.Disabled, contentHash);
        }

        var status = PolicyDocumentReader.GetStatus(root);
        if (status is PolicyStatus.Draft or PolicyStatus.Paused or PolicyStatus.Archived)
        {
            return PolicyDecision.Rejected(RejectionReason.Disabled, contentHash);
        }

        if (PolicyDocumentReader.TryGetWindow(root, out var from, out var to))
        {
            var now = _clock.UtcNow;
            if (from is not null && now < from.Value)
            {
                return PolicyDecision.Rejected(RejectionReason.NotYetActive, contentHash);
            }

            if (to is not null && now > to.Value)
            {
                return PolicyDecision.Rejected(RejectionReason.Expired, contentHash);
            }
        }

        var couponUsesTotal = await ResolveCouponUsesTotalAsync(policy.PartitionKey, cancellationToken)
            .ConfigureAwait(false);
        var couponUsesByCustomer = await _redemptions
            .CountConfirmedByCustomerAsync(policy.PartitionKey, customer.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        var scope = EvalScope.Create(
            _clock,
            cart,
            _registry,
            captureFullTrace: captureFullTrace,
            confirmedOrderCount: customer.ConfirmedOrderCount,
            isFirstOrder: customer.ConfirmedOrderCount == 0,
            couponUsesTotal: couponUsesTotal,
            couponUsesByCustomer: couponUsesByCustomer);

        var handle = _cache.GetOrAdd(
            policy.DocumentJson,
            () => CompileCondition(PolicyDocumentReader.GetConditionJson(root)));

        var conditionResult = await handle.Condition.Condition(scope, cancellationToken).ConfigureAwait(false);
        var trace = scope.Trace.ToEvaluationTrace();

        if (!conditionResult.GetBool())
        {
            var reason = scope.Trace.NearMisses.Count > 0
                ? RejectionReasonMapper.FromNearMiss(scope.Trace.NearMisses[0])
                : RejectionReason.Disabled;

            return PolicyDecision.Rejected(
                reason,
                contentHash,
                RejectionReasonMapper.ToHint(scope.Trace.NearMisses),
                trace);
        }

        if (status is PolicyStatus.Shadow)
        {
            return PolicyDecision.Rejected(RejectionReason.Disabled, contentHash, trace: trace);
        }

        var effect = PolicyDocumentReader.GetEffect(root);
        var effectScope = EffectEngine.CreateScope(scope);
        var plan = _effectApplier.Apply(effect, effectScope);

        return PolicyDecision.Applied(plan, contentHash);
    }

    private CompiledCondition CompileCondition(string conditionJson)
    {
        using var document = JsonDocument.Parse(conditionJson);
        var expression = PolicyParser.Parse(
            document.RootElement,
            new ParseBudget(EngineLimits.Default.MaxParseNodes, EngineLimits.Default.MaxParseDepth),
            PolicyValidator.ConditionPath);

        return _compiler.Compile(expression, _registry);
    }

    private async Task<int> ResolveCouponUsesTotalAsync(string partitionKey, CancellationToken cancellationToken)
    {
        var counter = await _redemptions.GetCounterAsync(partitionKey, cancellationToken).ConfigureAwait(false);
        return counter is null ? 0 : counter.ConfirmedCount + counter.ActiveReservations;
    }
}
