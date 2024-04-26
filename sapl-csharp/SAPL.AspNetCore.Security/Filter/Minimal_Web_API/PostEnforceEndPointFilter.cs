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
using Microsoft.AspNetCore.Mvc;
using SAPL.AspNetCore.Security.Filter.Base;
using SAPL.AspNetCore.Security.Method.Blocking;

namespace SAPL.AspNetCore.Security.Filter.Minimal_Web_API
{
    /// <summary>
    /// Endpoint filter for post-execution policy enforcement in a Minimal Web API context.
    /// It captures and potentially modifies the action result based on SAPL policies.
    /// </summary>
    public class PostEnforceEndPointFilter : SaplEndPointFilterBase
    {
        public object? ActionResultValue { get; private set; }
        public Type? ActionResultType { get; private set; }

        /// <summary>
        /// Initializes a new instance of the PostEnforceEndPointFilter class with specified SAPL policy attributes.
        /// </summary>
        public PostEnforceEndPointFilter(string subject, string action, string resource, string environment) : base(
            subject, action, resource, environment)
        {
        }

        /// <summary>
        /// Invoked asynchronously to apply post-execution enforcement logic on the endpoint's result.
        /// </summary>
        /// <param name="context">Context of the endpoint invocation.</param>
        /// <param name="next">Delegate to the next filter in the pipeline.</param>
        /// <returns>The potentially modified result object after policy enforcement.</returns>
        public override async ValueTask<object?> InvokeFilterAsync(EndpointFilterInvocationContext context,
            EndpointFilterDelegate next)
        {
            // Fetch and process the result of the endpoint execution
            var result = await next(context);
            // Determine the result type and value
            if (result is ObjectResult objectResult)
            {
                ActionResultValue = objectResult.Value;

                if (objectResult.DeclaredType != null)
                {
                    ActionResultType = objectResult.DeclaredType;
                }
                else if (objectResult.Value != null)
                {
                    ActionResultType = objectResult.Value.GetType();
                }
            }
            else if (result is OkObjectResult okObjectResult)
            {
                ActionResultValue = okObjectResult.Value;
                if (okObjectResult.DeclaredType != null)
                {
                    ActionResultType = okObjectResult.DeclaredType;
                }
                else if (okObjectResult.Value != null)
                {
                    ActionResultType = okObjectResult.Value.GetType();
                }
            }
            else
            {
                ActionResultValue = result;
                ActionResultType = result?.GetType();
            }

            // Create and use the PostEnforcePolicyEnforcementPoint to apply post-execution logic
            PostEnforcePolicyEnforcementPoint pdp = new PostEnforcePolicyEnforcementPoint(context.HttpContext,
                logger!,
                constraintEnforcementService,
                policyDecisionPoint,
                authorizationSubscriptionBuilderService);

            // Process the result with the policy enforcement point and return the possibly modified result
            var resultObject = await pdp.PermittedResult(ActionResultType, ActionResultValue);
            return resultObject;
        }
    }
}