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
using SAPL.AspNetCore.Security.Filter.Metadata;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Subscription
{
    /// <summary>
    /// Defines a contract for a service that creates AuthorizationSubscription objects.
    /// These objects represent the context (subject, action, resource, and environment)
    /// for an authorization decision in SAPL (Simple Attribute-based Policy Language).
    /// </summary>
    public interface IAuthorizationSubscriptionBuilderService
    {
        /// <summary>
        /// Creates an AuthorizationSubscription based on the current HTTP context and, optionally,
        /// a SAPL attribute.
        /// </summary>
        /// <param name="context">The HttpContext of the current request.</param>
        /// <param name="saplAttribute">Optional SAPL attribute to provide additional context for the subscription.</param>
        /// <returns>An AuthorizationSubscription object.</returns>
        public AuthorizationSubscription CreateAuthorizationSubscription(HttpContext context, ISaplAttribute? saplAttribute = null);
    }
}