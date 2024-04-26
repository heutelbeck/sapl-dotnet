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
using SAPL.AspNetCore.Security.Filter.Metadata;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Filter.Base
{
    /// <summary>
    /// Base class for creating endpoint filters that integrate SAPL for authorization decisions.
    /// This class implements IEndpointFilter and ISaplAttribute to support custom authorization logic.
    /// </summary>
    public abstract class SaplEndPointFilterBase : IEndpointFilter, ISaplAttribute
    {
        public string? Subject { get; set; }
        public string? Action { get; set; }
        public string? Resource { get; set; }
        public string? Environment { get; }

        protected ILogger? logger;
        protected IPolicyDecisionPoint? policyDecisionPoint;
        protected IAuthorizationSubscriptionBuilderService? authorizationSubscriptionBuilderService;
        protected IConstraintEnforcementService? constraintEnforcementService;

        /// <summary>
        /// Initializes a new instance of the SaplEndPointFilterBase class with specified subject, action, resource, and environment.
        /// </summary>
        protected SaplEndPointFilterBase(string subject, string action, string resource, string environment)
        {
            Subject = subject;
            Action = action;
            Resource = resource;
            Environment = environment;
        }

        /// <summary>
        /// Abstract method that must be implemented by derived classes to define filter logic.
        /// </summary>
        /// <param name="context">The context of the endpoint filter invocation.</param>
        /// <param name="next">Delegate to the next filter in the pipeline.</param>
        public abstract ValueTask<object?> InvokeFilterAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next);

        /// <summary>
        /// Sets attributes and necessary services to the HttpContext of the endpoint filter invocation context.
        /// </summary>
        /// <param name="context">The context of the endpoint filter invocation.</param>
        protected virtual void SetAttributesToContext(EndpointFilterInvocationContext context)
        {
            // Method implementation details...
        }

        /// <summary>
        /// Invokes the endpoint filter with additional setup for attributes and services.
        /// </summary>
        /// <param name="context">The context of the endpoint filter invocation.</param>
        /// <param name="next">Delegate to the next filter in the pipeline.</param>
        ValueTask<object?> IEndpointFilter.InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
        {
            SetAttributesToContext(context);
            return InvokeFilterAsync(context, next);
        }
    }
}
