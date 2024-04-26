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
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Method.Blocking
{
    /// <summary>
    /// Abstract base class for policy enforcement points in ASP.NET Core applications.
    /// It serves as a foundation for implementing specific policy enforcement logic.
    /// </summary>
    public abstract class PolicyEnforcementPointBase
    {
        protected HttpContext Context;
        protected readonly ILogger? Logger;
        protected IPolicyDecisionPoint? PolicyDecisionPoint;
        protected AuthorizationDecision? AuthorizationDecision;
        protected IConstraintEnforcementService? ConstraintEnforcementService;
        protected IAuthorizationSubscriptionBuilderService? AuthorizationSubscriptionBuilderService;

        /// <summary>
        /// Initializes a new instance of the PolicyEnforcementPointBase class.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        /// <param name="logger">The logger for logging information.</param>
        /// <param name="constraintEnforceService">Service for enforcing constraints.</param>
        /// <param name="policyDecisionPoint">The policy decision point for making authorization decisions.</param>
        /// <param name="authorizationSubscriptionBuilder">Service for building authorization subscriptions.</param>
        public PolicyEnforcementPointBase(HttpContext context,
            ILogger? logger,
            IConstraintEnforcementService? constraintEnforceService,
            IPolicyDecisionPoint? policyDecisionPoint,
            IAuthorizationSubscriptionBuilderService? authorizationSubscriptionBuilder)
        {
            this.Context = context;
            this.Logger = logger;
            if (constraintEnforceService != null)
            {
                this.ConstraintEnforcementService = constraintEnforceService;
            }
            else
            {
                this.ConstraintEnforcementService = constraintEnforceService;
            }
            this.PolicyDecisionPoint = policyDecisionPoint;
            this.AuthorizationSubscriptionBuilderService = authorizationSubscriptionBuilder;
        }
    }
}
