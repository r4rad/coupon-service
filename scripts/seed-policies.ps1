<#
.SYNOPSIS
    Seeds the deterministic demo policy set through the admin API.

.DESCRIPTION
    Idempotent (AC-9.5, AC-9.6): GET by policyId; POST when missing; PUT with If-Match when present.
    Safe to re-run; converges to the same Active documents without manual cleanup.

.PARAMETER BaseUrl
    Coupon Service base URL (direct backend preferred for CD). No trailing slash required.
    Do not use the customer APIM path `/coupons` — admin routes are not published there.

.PARAMETER BearerToken
    Admin-role bearer token. Prefer passing from an ADO secret variable; never commit a real token.

.EXAMPLE
    ./scripts/seed-policies.ps1 -BaseUrl https://ca-coupon-api-demo.example -BearerToken $token
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string] $BaseUrl,

    [Parameter(Mandatory = $true)]
    [string] $BearerToken
)

$ErrorActionPreference = 'Stop'

function Get-SeedPolicies {
    # Deterministic set from solution-architecture section 11.3.
    @(
        @{
            policyId = 'seed-save10'
            code = 'SAVE10'
            trigger = 'code'
            status = 'Active'
            engineSchema = '1.0'
            condition = @{ gte = @( @{ fact = 'cart.subtotal' }, 0 ) }
            effect = @{
                percentage = @{
                    value = 10
                    of = @{ lines = @{ where = @{ gte = @( @{ fact = 'line.quantity' }, 1 ) } } }
                }
            }
        },
        @{
            policyId = 'seed-flat5'
            code = 'FLAT5'
            trigger = 'code'
            status = 'Active'
            engineSchema = '1.0'
            condition = @{ gte = @( @{ fact = 'cart.subtotal' }, 20 ) }
            effect = @{ fixedAmount = @{ amount = 5.00 } }
        },
        @{
            policyId = 'seed-veggie15'
            code = 'VEGGIE15'
            trigger = 'code'
            status = 'Active'
            engineSchema = '1.0'
            condition = @{
                all = @(
                    @{ gte = @( @{ fact = 'cart.subtotal' }, 25.00 ) },
                    @{
                        every = @{
                            over = 'cart.lines'
                            where = @{ eq = @( @{ fact = 'line.category' }, 'Vegetarian' ) }
                        }
                    },
                    @{
                        any = @(
                            @{ eq = @( @{ fact = 'customer.confirmedOrderCount' }, 0 ) },
                            @{ in = @( @{ fact = 'time.localDayOfWeek' }, @('Saturday', 'Sunday') ) }
                        )
                    }
                )
            }
            effect = @{
                cap = @{
                    max = 10.00
                    of = @{
                        percentage = @{
                            value = 15
                            of = @{
                                lines = @{
                                    where = @{ eq = @( @{ fact = 'line.category' }, 'Vegetarian' ) }
                                }
                            }
                        }
                    }
                }
            }
        },
        @{
            policyId = 'seed-bogo'
            code = 'BOGO'
            trigger = 'code'
            status = 'Active'
            engineSchema = '1.0'
            condition = @{ gte = @( @{ fact = 'cart.totalQuantity' }, 2 ) }
            effect = @{
                nthItem = @{
                    n = 2
                    percentage = 100
                    from = @{
                        lines = @{
                            where = @{ gte = @( @{ fact = 'line.quantity' }, 1 ) }
                        }
                    }
                }
            }
        },
        @{
            policyId = 'seed-either'
            code = 'EITHER'
            trigger = 'code'
            status = 'Active'
            engineSchema = '1.0'
            condition = @{ gte = @( @{ fact = 'cart.subtotal' }, 0 ) }
            effect = @{
                bestOf = @(
                    @{
                        percentage = @{
                            value = 15
                            of = @{
                                lines = @{
                                    where = @{ gte = @( @{ fact = 'line.quantity' }, 1 ) }
                                }
                            }
                        }
                    },
                    @{ fixedAmount = @{ amount = 5.00 } }
                )
            }
        },
        @{
            policyId = 'seed-oldcode'
            code = 'OLDCODE'
            trigger = 'code'
            status = 'Active'
            engineSchema = '1.0'
            window = @{ to = '2020-01-01T00:00:00Z' }
            condition = @{ gte = @( @{ fact = 'cart.subtotal' }, 0 ) }
            effect = @{
                percentage = @{
                    value = 10
                    of = @{ lines = @{ where = @{ gte = @( @{ fact = 'line.quantity' }, 1 ) } } }
                }
            }
        },
        @{
            policyId = 'seed-limited1'
            code = 'LIMITED1'
            trigger = 'code'
            status = 'Active'
            engineSchema = '1.0'
            limits = @{ totalUses = 1 }
            condition = @{ lt = @( @{ fact = 'coupon.uses.total' }, 1 ) }
            effect = @{
                percentage = @{
                    value = 10
                    of = @{ lines = @{ where = @{ gte = @( @{ fact = 'line.quantity' }, 1 ) } } }
                }
            }
        },
        @{
            policyId = 'seed-tuesday10'
            trigger = 'automatic'
            status = 'Active'
            priority = 100
            stackable = $false
            engineSchema = '1.0'
            condition = @{ eq = @( @{ fact = 'time.localDayOfWeek' }, 'Tuesday' ) }
            effect = @{
                percentage = @{
                    value = 10
                    of = @{ lines = @{ where = @{ gte = @( @{ fact = 'line.quantity' }, 1 ) } } }
                }
            }
        }
    )
}

function ConvertTo-PolicyJson {
    param([hashtable] $Policy)
    # ConvertTo-Json -Depth keeps nested effect/condition trees intact.
    return ($Policy | ConvertTo-Json -Depth 20 -Compress)
}

function Invoke-AdminJson {
    param(
        [string] $Method,
        [string] $Uri,
        [string] $Body = $null,
        [hashtable] $Headers = @{},
        [int[]] $AllowedStatusCodes = @(200, 201, 204)
    )

    $allHeaders = @{
        Authorization = "Bearer $BearerToken"
    }
    foreach ($key in $Headers.Keys) {
        $allHeaders[$key] = $Headers[$key]
    }

    $statusCode = 0
    $params = @{
        Method             = $Method
        Uri                = $Uri
        Headers            = $allHeaders
        ContentType        = 'application/json'
        ErrorAction        = 'Stop'
        # pwsh 7: do not throw on 404/4xx so callers can branch on status (idempotent GET).
        SkipHttpErrorCheck = $true
        StatusCodeVariable = 'statusCode'
    }
    if ($null -ne $Body) {
        $params['Body'] = $Body
    }

    $result = Invoke-RestMethod @params
    if ($AllowedStatusCodes -notcontains [int]$statusCode) {
        $detail = if ($null -eq $result) { '' } else { ($result | ConvertTo-Json -Compress -Depth 10) }
        $hint = ''
        if ([int]$statusCode -eq 401 -or [int]$statusCode -eq 403) {
            $hint = ' Check Admin bearer: deployed hosts require an Entra JWT with roles=Coupon.Admin (TestToken is disabled).'
        }
        throw "Admin API $Method $Uri failed with HTTP $statusCode.$hint $detail"
    }

    return [pscustomobject]@{
        StatusCode = [int]$statusCode
        Body       = $result
    }
}

if ([string]::IsNullOrWhiteSpace($BaseUrl)) {
    throw 'BaseUrl is empty; pass an absolute http(s) admin API base URL.'
}

$root = $BaseUrl.TrimEnd('/')
$parsedRoot = $null
if (-not [Uri]::TryCreate($root, [UriKind]::Absolute, [ref]$parsedRoot) -or
    ($parsedRoot.Scheme -ne [Uri]::UriSchemeHttp -and $parsedRoot.Scheme -ne [Uri]::UriSchemeHttps)) {
    throw "BaseUrl is not an absolute http(s) URI: '$BaseUrl'"
}

$policies = Get-SeedPolicies
$created = 0
$updated = 0

foreach ($policy in $policies) {
    $policyId = [string]$policy.policyId
    $uri = "$root/v1/admin/policies/$([uri]::EscapeDataString($policyId))"
    $json = ConvertTo-PolicyJson -Policy $policy

    $get = Invoke-AdminJson -Method Get -Uri $uri -AllowedStatusCodes @(200, 404)

    if ([int]$get.StatusCode -eq 404) {
        $createUri = "$root/v1/admin/policies"
        Invoke-AdminJson -Method Post -Uri $createUri -Body $json -AllowedStatusCodes @(200, 201) | Out-Null
        Write-Host "Created policy $policyId"
        $created++
    }
    else {
        $existing = $get.Body
        $etag = $null
        if ($null -ne $existing) {
            if ($existing.PSObject.Properties.Name -contains 'etag') {
                $etag = [string]$existing.etag
            }
            elseif ($existing.PSObject.Properties.Name -contains 'eTag') {
                $etag = [string]$existing.eTag
            }
        }
        if ([string]::IsNullOrWhiteSpace($etag)) {
            throw "Admin GET for $policyId returned HTTP 200 with no etag; cannot PUT idempotently."
        }
        Invoke-AdminJson -Method Put -Uri $uri -Body $json -Headers @{ 'If-Match' = $etag } -AllowedStatusCodes @(200) | Out-Null
        Write-Host "Updated policy $policyId"
        $updated++
    }
}

Write-Host "Seed complete: $created created, $updated updated, $($policies.Count) total."