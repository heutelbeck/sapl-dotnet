/*
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 * 
 *      http://www.apache.org/licenses/LICENSE-2.0
 * 
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.AspNetCore.Security.Filter.Base;
using SAPL.AspNetCore.Security.Method.Blocking;

namespace SAPL.AspNetCore.Security.Filter.Minimal_Web_API;

/// <summary>
/// Endpoint filter for pre-execution policy enforcement in a Minimal Web API context.
/// It checks whether access to the endpoint is permitted based on SAPL policies.
/// </summary>
public class PreEnforceEndPointFilter : SaplEndPointFilterBase
{
    /// <summary>
    /// Initializes a new instance of the PreEnforceEndPointFilter class with specified SAPL policy attributes.
    /// </summary>
    public PreEnforceEndPointFilter(string subject, string action, string resource, string environment) : base(subject, action, resource, environment)
    {
    }

    /// <summary>
    /// Invoked asynchronously to apply pre-execution enforcement logic on the endpoint.
    /// </summary>
    /// <param name="context">Context of the endpoint invocation.</param>
    /// <param name="next">Delegate to the next filter in the pipeline.</param>
    /// <returns>The original or altered result object after applying the policy enforcement.</returns>
    public override async ValueTask<object?> InvokeFilterAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        // Create a policy enforcement point for pre-execution logic
        PreEnforcePolicyEnforcementPoint pdp = new PreEnforcePolicyEnforcementPoint(context.HttpContext,
              logger,
              constraintEnforcementService,
              policyDecisionPoint,
              authorizationSubscriptionBuilderService);

        // Check if access to the endpoint is permitted
        if (await pdp.IsAccessPermitted(null!))
        {
            logger!.Log(LogLevel.Information, $"Access granted on Endpoint.");
        }
        else
        {
            // Access denied; throw an exception to halt the request processing
            throw new AccessDeniedException();
        }

        // Continue with the next filter in the pipeline
        var result = await next(context);
        return result;
    }
}
