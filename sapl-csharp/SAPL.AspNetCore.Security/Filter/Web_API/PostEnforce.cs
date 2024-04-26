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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.AspNetCore.Security.Filter.Base;
using SAPL.AspNetCore.Security.Filter.Metadata;
using SAPL.AspNetCore.Security.Method.Blocking;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Filter.Web_API
{
    /// <summary>
    /// An attribute that enforces SAPL-based security policies after an action's execution
    /// but before the result is processed.
    /// </summary>
    public class PostEnforce : Attribute, IPostEnforce
    {
        private ILogger? logger;
        private IPolicyDecisionPoint? policyDecisionPoint;
        private IAuthorizationSubscriptionBuilderService? authorizationSubscriptionBuilderService;
        IConstraintEnforcementService? constraintEnforcementService;

        public string? Subject { get; set; }
        public string? Action { get; set; }
        public string? Resource { get; set; }
        public string? Environment { get; set; }
        private IActionResult? ActionResult { get; set; }
        public object? ActionResultValue { get; private set; }
        public Type? ActionResultType { get; private set; }

        public PostEnforce(string? subject = null, string? action = null, string? resource = null,
            string? environment = null)
        {
            Subject = subject;
            Action = action;
            Resource = resource;
            Environment = environment;
        }

        /// <summary>
        /// Called asynchronously after the action method but before the result is processed.
        /// It applies the post-execution policy enforcement logic based on SAPL.
        /// </summary>
        /// <param name="context">Context for the executing result.</param>
        /// <param name="next">Delegate to execute the next result filter or result processor.</param>
        public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
        {
            ExtractTypeAndValueOfResult(context);
            var result = await EnforcePermittedResult(context);
            SetResultToObjectResult(context, result);
            await next();
        }

        private void ExtractTypeAndValueOfResult(ResultExecutingContext context)
        {
            ActionResult = context.Result;
            if (ActionResult is ObjectResult objectResult)
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
            else if (ActionResult is OkObjectResult okObjectResult)
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
        }

        private static void SetResultToObjectResult(ResultExecutingContext context, object? result)
        {
            if (context.Result is ObjectResult objectResult1)
            {
                objectResult1.Value = result;
            }
            else if (context.Result is OkObjectResult okObjectResult)
            {
                okObjectResult.Value = result;
            }
        }

        private async Task<object?> EnforcePermittedResult(ResultExecutingContext context)
        {
            object? result;
            try
            {
                ExtractRequiredSaplServices(context);
                var constraintHandler =
                    context.HttpContext.RequestServices.GetServices<IResponsibleConstraintHandlerProvider>();
                constraintEnforcementService =
                    new ConstraintEnforcementService(constraintHandler);
                if (logger == null || constraintEnforcementService == null || policyDecisionPoint == null || authorizationSubscriptionBuilderService == null)
                {
                    throw new AccessDeniedException("Extracting required DI-Service for SAPL failed");
                }
                PostEnforcePolicyEnforcementPoint pdp = new PostEnforcePolicyEnforcementPoint(context.HttpContext,
                    logger,
                    constraintEnforcementService,
                    policyDecisionPoint,
                    authorizationSubscriptionBuilderService);
                result = await pdp.PermittedResult(ActionResultType, ActionResultValue);
                return result;
            }
            catch (Exception)
            {
                result = null;
            }
            return result;
        }

        private void ExtractRequiredSaplServices(ResultExecutingContext context)
        {
            ILoggerFactory? loggerFactory = context.HttpContext.RequestServices.GetService<ILoggerFactory>();
            if (loggerFactory != null)
            {
                logger = loggerFactory.CreateLogger<SaplPreEnforceAttributeBase>();
            }
            policyDecisionPoint = context.HttpContext.RequestServices.GetRequiredService<IPolicyDecisionPoint>();
            authorizationSubscriptionBuilderService = context.HttpContext.RequestServices
                .GetRequiredService<IAuthorizationSubscriptionBuilderService>();
        }
    }

}
