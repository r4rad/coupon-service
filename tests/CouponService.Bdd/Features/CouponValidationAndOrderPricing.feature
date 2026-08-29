Feature: Coupon validation and order pricing

  Scenario: Percentage coupon reduces the total
    Given a cart with 2 x "Margherita" at 9.50 and 1 x "BBQ Chicken" at 12.00
    And an active policy "SAVE10" giving 10 percent off
    When the customer previews the coupon
    Then the subtotal is 31.00 and the discount is 3.10 and the total is 27.90

  Scenario: Expired coupon is reported, not thrown
    Given a policy "OLDCODE" whose window ended yesterday
    Then the response status is 200 and the reason is "Expired"

  Scenario: A rejection tells the customer how close they were
    Given a cart totalling 21.90 and a policy requiring a minimum of 25.00
    Then the reason is "MinimumOrderNotMet" and the hint shortfall is 3.10

  Scenario: A new rule ships without a deployment
    Given a new policy created via the admin API with condition
      """
      { "gte": [ { "fact": "cart.lineCount" }, 3 ] }
      """
    When a cart with 3 lines previews that policy
    Then the coupon status is "Applied" and no service was redeployed

  Scenario: A capped best-of offer picks the larger discount and stops at the ceiling
    Given a cart totalling 200.00
    And a policy offering the better of 15 percent or 5.00 flat, capped at 10.00
    Then the discount is 10.00 and the allocations sum to 10.00

  Scenario: An automatic policy applies with no code entered
    Given an active automatic policy "TUESDAY10" and today is Tuesday
    When the customer previews with no coupon code
    Then a 10 percent discount is applied

  Scenario: A policy referencing an unknown fact is rejected on write
    When an administrator submits a condition referencing "customer.zodiacSign"
    Then the response status is 400 and the error identifies the unknown fact

  Scenario: A shadow policy is evaluated but never discounts
    Given a policy "TRIAL20" in Shadow status that would apply
    Then the discount is 0.00
    And a "PolicyShadowEvaluated" event records what it would have given

  Scenario: Usage limit is enforced across concurrent checkouts
    Given a policy "LIMITED1" with a maximum usage of 1
    When two orders reserve "LIMITED1" at the same time
    Then exactly one succeeds and the other is rejected with "UsageLimitReached"

  Scenario: Client-side tampering is ignored
    Given a cart whose true total is 31.00
    When the client submits the order claiming a total of 1.00
    Then the stored order total is 27.90 with coupon "SAVE10"

  Scenario: Mutation endpoints reject a customer token
    Given a valid customer token without the "Coupon.Redeem" role
    When the reservations endpoint is called
    Then the response status is 403
