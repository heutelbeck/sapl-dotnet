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

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Filter.Metadata;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Subscription
{
    /// <summary>
    /// Utility class providing methods to assist in constructing the context for SAPL authorization decisions.
    /// </summary>
    public class AuthorizationSubscriptionUtil
    {
        /// <summary>
        /// Checks if the current HTTP context is secured by a SAPL attribute.
        /// </summary>
        /// <param name="context">The HttpContext of the current request.</param>
        /// <returns>True if a SAPL attribute is present, false otherwise.</returns>
        public static bool IsSaplSecure(HttpContext context)
        {
            var controllerActionDescriptor = context.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata?
                .GetMetadata<ControllerActionDescriptor>();
            if (controllerActionDescriptor != null)
            {
                foreach (FilterDescriptor descriptor in controllerActionDescriptor.FilterDescriptors)
                {
                    var filter = descriptor.Filter;
                    if (filter is ISaplAttribute)
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Retrieves the SAPL attribute from the current HTTP context, if present.
        /// </summary>
        /// <param name="context">The HttpContext of the current request.</param>
        /// <returns>The SAPL attribute, or null if not found.</returns>
        public static ISaplAttribute? GetSaplAttribute(HttpContext context)
        {
            var controllerActionDescriptor = context.Features.Get<IEndpointFeature>()?.Endpoint?.Metadata?
                .GetMetadata<ControllerActionDescriptor>();
            if (controllerActionDescriptor != null)
            {
                foreach (FilterDescriptor descriptor in controllerActionDescriptor.FilterDescriptors)
                {
                    var filter = descriptor.Filter;
                    if (filter is ISaplAttribute attribute)
                    {
                        return attribute;
                    }
                }
            }
            return null;
        }

        /// <summary>
        /// Constructs a JObject representing the environment from the hosting environment.
        /// </summary>
        /// <param name="hostingEnvironment">The IWebHostEnvironment instance.</param>
        /// <returns>A JObject with environment details.</returns>
        public static JObject GetEnvironmentFromHostingEnvironment(IWebHostEnvironment? hostingEnvironment)
        {
            var environment = new JObject
            {
                { nameof(hostingEnvironment.EnvironmentName), hostingEnvironment?.EnvironmentName },
                { nameof(hostingEnvironment.ApplicationName), hostingEnvironment?.ApplicationName },
                { nameof(hostingEnvironment.ContentRootPath), hostingEnvironment?.ContentRootPath },
                { nameof(hostingEnvironment.WebRootPath), hostingEnvironment?.WebRootPath }
            };
            return environment;
        }

        /// <summary>
        /// Attempts to retrieve the action details from the current HTTP context.
        /// </summary>
        /// <param name="context">The HttpContext of the current request.</param>
        /// <returns>A JObject with action details, or null if not found.</returns>
        public static JObject? TryGetActionFromHttpContext(HttpContext context)
        {
            var controllerActionDescriptor = context.Features.Get<IEndpointFeature>()?.Endpoint!.Metadata
                  .GetMetadata<ControllerActionDescriptor>();
            if (controllerActionDescriptor != null)
            {
                var action = new JObject
                {
                    { nameof(controllerActionDescriptor.ActionName), controllerActionDescriptor.ActionName },
                    { nameof(controllerActionDescriptor.ControllerName), controllerActionDescriptor.ControllerName }
                    //{ nameof(controllerActionDescriptor.ControllerTypeInfo.FullName), controllerActionDescriptor.ControllerTypeInfo.FullName }
                };
                return action;
            }
            return null;
        }

        /// <summary>
        /// Constructs a JObject representing the resource from the current HTTP context.
        /// </summary>
        /// <param name="context">The HttpContext of the current request.</param>
        /// <returns>A JObject with resource details.</returns>
        public static JObject GetResourceFromHttpContext(HttpContext context)
        {
            RemoveSubscriptionAttributes(context, nameof(AuthorizationSubscription.Resource));
            var resource = new JObject
            {
                { nameof(context.Request.Path), context.Request.Path.Value },
                { nameof(context.Request.ContentType), context.Request.ContentType },
                { nameof(context.Request.Host), context.Request.Host.Value }
            };
            return resource;
        }

        /// <summary>
        /// Removes specified subscription attributes from the current HTTP context.
        /// </summary>
        /// <param name="context">The HttpContext of the current request.</param>
        /// <param name="attributeName">The name of the attribute to remove.</param>
        private static void RemoveSubscriptionAttributes(HttpContext context, string attributeName)
        {
            if (context.Items.ContainsKey(attributeName))
            {
                context.Items.Remove(attributeName);
            }
        }
    }
}
