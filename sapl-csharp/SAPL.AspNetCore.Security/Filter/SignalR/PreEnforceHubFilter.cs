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

using System.ComponentModel;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SAPL.AspNetCore.Security.Constraints;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Filter.Web_API;
using SAPL.AspNetCore.Security.Method.Blocking;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Filter.SignalR;

/// <summary>
/// Implements a SignalR Hub filter for enforcing SAPL-based security policies before hub methods are executed.
/// </summary>
public class PreEnforceHubFilter : IHubFilter
{
    private ILogger? logger;
    private IPolicyDecisionPoint? policyDecisionPoint;
    private IAuthorizationSubscriptionBuilderService? authorizationSubscriptionBuilderService;
    private bool accessPermitted;
    private PreEnforcePolicyEnforcementPoint? policyEnforcementPoint;
    IConstraintEnforcementService? constraintEnforcementService;

    /// <summary>
    /// Intercepts hub method invocations to apply pre-execution enforcement logic.
    /// </summary>
    /// <param name="invocationContext">Context of the hub invocation.</param>
    /// <param name="next">Delegate to the next filter in the pipeline.</param>
    /// <returns>A task representing the asynchronous operation of method invocation.</returns>
    async ValueTask<object?> IHubFilter.InvokeMethodAsync(HubInvocationContext invocationContext,
        Func<HubInvocationContext, ValueTask<object?>> next)
    {
        var preEnforceFilter = Attribute.GetCustomAttribute(
            invocationContext.HubMethod, typeof(PreEnforce)) as PreEnforce;
        if (preEnforceFilter == null)
        {
            if (string.IsNullOrEmpty(nameof(PreEnforce.Action)))
            {
                preEnforceFilter!.Action = preEnforceFilter.CallingMethodName;
            }
        }

        if (invocationContext.ServiceProvider.GetService(typeof(ILoggerFactory)) is ILoggerFactory loggerFactory)
        {
            logger = loggerFactory.CreateLogger<PreEnforce>();
        }

        policyDecisionPoint =
            invocationContext.ServiceProvider.GetService(typeof(IPolicyDecisionPoint)) as IPolicyDecisionPoint;
        authorizationSubscriptionBuilderService =
            invocationContext.ServiceProvider.GetService(typeof(IAuthorizationSubscriptionBuilderService)) as
                IAuthorizationSubscriptionBuilderService;

        var constraintHandler =
            invocationContext.ServiceProvider.GetServices(typeof(IResponsibleConstraintHandlerProvider));
        constraintEnforcementService =
            new ConstraintEnforcementService(constraintHandler.Select(a => a as IResponsibleConstraintHandlerProvider));

        policyEnforcementPoint = new PreEnforcePolicyEnforcementPoint(preEnforceFilter,
            logger,
            constraintEnforcementService,
            policyDecisionPoint,
            authorizationSubscriptionBuilderService);

        if (policyDecisionPoint != null)
        {
            policyDecisionPoint.SubscriptionCacheUpdated += PolicyDecisionPointOnSubscriptionCacheUpdated;
        }

        accessPermitted = await policyEnforcementPoint.IsAccessPermitted(null!);

        while (!accessPermitted)
        {
            if (logger != null)
            {
                logger.Log(LogLevel.Warning, $"Access Denied");
            }
        }

        if (logger != null)
        {
            logger.Log(LogLevel.Information, $"Access granted on Endpoint.");
        }
        return next(invocationContext);
    }

    /// <summary>
    /// Responds to policy decision point updates to reassess access permissions.
    /// </summary>
    private async void PolicyDecisionPointOnSubscriptionCacheUpdated(object? sender, PropertyChangedEventArgs e)
    {
        if (policyEnforcementPoint != null) accessPermitted = await policyEnforcementPoint.IsAccessPermitted(null!);
    }

}