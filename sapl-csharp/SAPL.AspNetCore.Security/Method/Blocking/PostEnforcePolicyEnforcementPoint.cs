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
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Method.Blocking
{
    /// <summary>
    /// Enforces security policies after an action has executed in ASP.NET Core applications.
    /// This class is a concrete implementation of a policy enforcement point that operates post-action execution.
    /// </summary>
    public class PostEnforcePolicyEnforcementPoint : PolicyEnforcementPointBase
    {
        IConstraintEnforcementService? constraintEnforcementService;

        /// <summary>
        /// Initializes a new instance of the PostEnforcePolicyEnforcementPoint.
        /// </summary>
        public PostEnforcePolicyEnforcementPoint(HttpContext context, ILogger logger,
            IConstraintEnforcementService? constraintEnforceService, IPolicyDecisionPoint? policyDecisionPoint,
            IAuthorizationSubscriptionBuilderService? authorizationSubscriptionBuilder) : base(context, logger,
            constraintEnforceService, policyDecisionPoint, authorizationSubscriptionBuilder)
        {
            this.constraintEnforcementService = constraintEnforceService;
        }

        /// <summary>
        /// Evaluates and enforces the security policy on the given result, potentially modifying it.
        /// </summary>
        /// <param name="resultType">The type of the result.</param>
        /// <param name="resultValue">The value of the result.</param>
        /// <returns>The possibly modified result after policy enforcement.</returns>
        public async Task<object?> PermittedResult(Type? resultType, object? resultValue)
        {
            if (resultType == null || resultValue == null)
            {
                return null;
            }

            IBlockingPostEnforceConstraintHandlerBundle? bundle;
            var subscription = AuthorizationSubscriptionBuilderService!.CreateAuthorizationSubscription(Context);
            Logger!.Log(LogLevel.Information, subscription.ToString());

            AuthorizationDecision = await PolicyDecisionPoint!.Decide(subscription);

            if (AuthorizationDecision == null)
            {
                Logger!.Log(LogLevel.Error,
                    $"Access Denied by PEP. {nameof(SAPL.PDP.Api.AuthorizationDecision)} is null");
                throw new AccessDeniedException();
            }

            Logger!.Log(LogLevel.Information, AuthorizationDecision.DecisionString);
            try
            {
                bundle = constraintEnforcementService?.BlockingPostEnforceConstraintHandlerBundle(resultType,
                    resultValue, AuthorizationDecision);
            }
            catch (Exception e)
            {
                throw new AccessDeniedException(e, "Access Denied by PEP. Failed to construct bundle.");
            }


            if (bundle == null)
            {
                Logger!.Log(LogLevel.Warning, "Access Denied by PEP. No constraint handler bundle.");
                throw new AccessDeniedException();
            }

            try
            {
                bundle.HandleOnDecisionConstraints();
                if (AuthorizationDecision.Decision != Decision.PERMIT)
                {
                    throw new AccessDeniedException("Access denied by PDP");
                }

                return bundle.HandleAllConstraints();

            }
            catch (Exception e)
            {
                var handledException = bundle.HandleAllOnErrorConstraints(e);
                if (handledException != null)
                {
                    throw handledException;
                }
                throw new AccessDeniedException(e, "Access Denied by PEP. Failed to enforce decision");
            }
        }
    }
}
