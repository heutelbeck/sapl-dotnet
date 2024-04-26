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
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Authentication.Metadata;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.AspNetCore.Security.Extensions;
using SAPL.AspNetCore.Security.Filter.Metadata;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Subscription
{
    /// <summary>
    /// Service for creating an AuthorizationSubscription, which represents the context
    /// for an authorization decision, including subject, action, resource, and environment.
    /// </summary>
    public class AuthorizationSubscriptionBuilderService : IAuthorizationSubscriptionBuilderService
    {
        // Fields to store subject, action, resource, and environment
        private JToken? subject;
        private JToken? action;
        private JToken? resource;
        private JToken? environment;
        private ISaplAttribute? attributesFromFilter;
        private ISubjectFromAuthenticationBuilder? saplSubjectBuilder;
        private IWebHostEnvironment? hostingEnvironment;

        // Constructors
        public AuthorizationSubscriptionBuilderService() { }

        public AuthorizationSubscriptionBuilderService(
            ISubjectFromAuthenticationBuilder? subjectBuilder,
            IWebHostEnvironment? hostingEnvironment)
        {
            this.hostingEnvironment = hostingEnvironment;
            this.saplSubjectBuilder = subjectBuilder;

        }

        /// <summary>
        /// Creates an AuthorizationSubscription based on the current HTTP context and provided SAPL attributes.
        /// </summary>
        /// <param name="httpContext">The current HTTP context.</param>
        /// <param name="attribute">Optional SAPL attribute for additional context.</param>
        /// <returns>An AuthorizationSubscription instance.</returns>
        public AuthorizationSubscription CreateAuthorizationSubscription(HttpContext? httpContext, ISaplAttribute? attribute = null)
        {
            if (httpContext == null && attribute != null)
            {
                this.attributesFromFilter = attribute;
            }

            else if (httpContext != null)
            {
                //First try to get the Properties from FilterAttribute
                attributesFromFilter = AuthorizationSubscriptionUtil.GetSaplAttribute(httpContext);
            }

            if (!string.IsNullOrEmpty(attributesFromFilter?.Subject))
            {
                subject = attributesFromFilter.Subject;
            }
            else if (httpContext != null && saplSubjectBuilder != null)
            {
                subject = saplSubjectBuilder.GetSubject(httpContext);
            }
            else
            {
                subject = JValue.CreateString(httpContext!.GetItem(nameof(AuthorizationSubscription.Subject)));
            }

            if (string.IsNullOrEmpty(attributesFromFilter?.Action) && httpContext != null)
            {
                //Try because if Endpoint-Routing is not possible, the Action can not be set
                action = AuthorizationSubscriptionUtil.TryGetActionFromHttpContext(httpContext);
                if (action == null)
                {
                    action = JValue.CreateString(httpContext.GetItem(nameof(AuthorizationSubscription.Action)));
                }

            }
            else
            {
                action = JValue.CreateString(attributesFromFilter?.Action);
            }

            if (string.IsNullOrEmpty(attributesFromFilter?.Resource) && httpContext != null)
            {
                resource = AuthorizationSubscriptionUtil.GetResourceFromHttpContext(httpContext);
                if (resource == null)
                {
                    resource = JValue.CreateString(httpContext.GetItem(nameof(AuthorizationSubscription.Resource)));
                }
            }
            else
            {
                resource = JValue.CreateString(attributesFromFilter?.Resource);
            }

            if (string.IsNullOrEmpty(attributesFromFilter?.Environment) && hostingEnvironment != null)
            {
                environment = AuthorizationSubscriptionUtil.GetEnvironmentFromHostingEnvironment(hostingEnvironment);
                if (environment == null)
                {
                    environment = JValue.CreateString(httpContext!.GetItem(nameof(AuthorizationSubscription.Environment)));
                }
            }
            else
            {
                environment = JValue.CreateString(attributesFromFilter?.Environment);
            }

            if (subject == null)
            {
                throw new AccessDeniedException();
            }

            return new AuthorizationSubscription(subject, action, resource, environment);
        }
    }
}
