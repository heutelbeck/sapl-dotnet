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
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.Logging;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.AspNetCore.Security.Filter.Metadata;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Method.Blocking
{
    /// <summary>
    /// Enforces security policies before the execution of an action or endpoint in ASP.NET Core applications.
    /// This class is used to determine if access should be permitted based on SAPL policies.
    /// </summary>
    public class PreEnforcePolicyEnforcementPoint : PolicyEnforcementPointBase
    {
        private BlockingPreEnforceConstraintHandlerBundle? blockingPreEnforceBundle;
        private ISaplAttribute? attribute;

        public PreEnforcePolicyEnforcementPoint(HttpContext context, ILogger? logger, IConstraintEnforcementService? constraintEnforceService, IPolicyDecisionPoint? policyDecisionPoint, IAuthorizationSubscriptionBuilderService? authorizationSubscriptionBuilder) : base(context, logger, constraintEnforceService, policyDecisionPoint, authorizationSubscriptionBuilder)
        {
        }
        public PreEnforcePolicyEnforcementPoint(ISaplAttribute? attribute, ILogger? logger, IConstraintEnforcementService? constraintEnforceService, IPolicyDecisionPoint? policyDecisionPoint, IAuthorizationSubscriptionBuilderService? authorizationSubscriptionBuilder) : base(null!, logger, constraintEnforceService, policyDecisionPoint, authorizationSubscriptionBuilder)
        {
            this.attribute = attribute;
        }

        /// <summary>
        /// Determines if access is permitted to an action based on the SAPL policies.
        /// </summary>
        /// <param name="actionExecutingContext">The context of the action being executed.</param>
        /// <returns>True if access is permitted, false otherwise.</returns>
        public async Task<bool> IsAccessPermitted(ActionExecutingContext actionExecutingContext)
        {
            AuthorizationDecision = await PolicyDecisionPoint!.Decide(AuthorizationSubscriptionBuilderService!.CreateAuthorizationSubscription(Context, attribute));

            if (AuthorizationDecision == null)
            {
                Logger!.Log(LogLevel.Error, $"Access Denied by PEP. {nameof(SAPL.PDP.Api.AuthorizationDecision)} is null");
                throw new AccessDeniedException();
            }


            if (AuthorizationDecision.HasResourceReplacement)
            {
                Logger!.Log(LogLevel.Warning,
                    "Access Denied by PEP. @PreEnforce cannot replace method return value.");
                throw new AccessDeniedException();
            }

            blockingPreEnforceBundle =
                ConstraintEnforcementService!.BlockingPreEnforceBundleFor(AuthorizationDecision);
            if (blockingPreEnforceBundle == null)
            {
                Logger!.Log(LogLevel.Warning, "Access Denied by PEP. No constraint handler bundle.");
                throw new AccessDeniedException();
            }

            blockingPreEnforceBundle.HandleOnDecisionConstraints();

            if (actionExecutingContext != null)
            {
                blockingPreEnforceBundle.HandleMethodInvocationHandlers(actionExecutingContext);
            }

            return AuthorizationDecision.Decision == Decision.PERMIT;
        }
    }
}
