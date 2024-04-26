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

using System.Runtime.CompilerServices;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.AspNetCore.Security.Filter.Metadata;
using SAPL.AspNetCore.Security.Method.Blocking;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Filter.Base
{

    /// <summary>
    /// Base class for attributes that enforce security policies before the execution of an action.
    /// Derive from this class to create custom attributes that apply SAPL pre-enforcement logic to actions.
    /// </summary>
    public abstract class SaplPreEnforceAttributeBase : Attribute, IPreEnforce
    {

        private ILogger? logger;
        private IPolicyDecisionPoint? policyDecisionPoint;
        private IAuthorizationSubscriptionBuilderService? authorizationSubscriptionBuilderService;
        IConstraintEnforcementService? constraintEnforcementService;

        public string? CallingMethodName { get; set; }
        public string? Subject { get; set; }
        public string? Action { get; set; }
        public string? Resource { get; set; }
        public string? Environment { get; set; }

        /// <summary>
        /// Initializes a new instance of the SaplPreEnforceAttributeBase class.
        /// </summary>
        /// <param name="caller">The name of the method that applies this attribute, captured automatically.</param>
        protected SaplPreEnforceAttributeBase([CallerMemberName] string? caller = null)
        {
            CallingMethodName = caller;
        }

        /// <summary>
        /// Intercepts the action execution to perform authorization checks based on SAPL policies.
        /// </summary>
        /// <param name="context">Context for the executing action.</param>
        /// <param name="next">Delegate to execute the next action in the pipeline.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public async Task OnActionExecutionAsync(ActionExecutingContext context, ActionExecutionDelegate next)
        {

            ILoggerFactory? loggerFactory = context.HttpContext.RequestServices.GetService<ILoggerFactory>();
            if (loggerFactory != null)
            {
                this.logger = loggerFactory.CreateLogger<SaplPreEnforceAttributeBase>();
            }

            this.policyDecisionPoint = context.HttpContext.RequestServices.GetRequiredService<IPolicyDecisionPoint>();
            this.authorizationSubscriptionBuilderService = context.HttpContext.RequestServices
                .GetRequiredService<IAuthorizationSubscriptionBuilderService>();

            var constraintHandler = context.HttpContext.RequestServices.GetServices<IResponsibleConstraintHandlerProvider>();
            this.constraintEnforcementService = new ConstraintEnforcementService(constraintHandler);

            PreEnforcePolicyEnforcementPoint pdp = new PreEnforcePolicyEnforcementPoint(context.HttpContext,
                logger,
                constraintEnforcementService,
                policyDecisionPoint,
                authorizationSubscriptionBuilderService);

            if (await pdp.IsAccessPermitted(context))
            {
                logger!.Log(LogLevel.Information, $"Access granted on Endpoint.");
            }
            else
            {
                throw new AccessDeniedException();
            }
            await next();
        }
    }
}
