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

using System.Composition;
using SAPL.PDP.Api;

namespace SAPL.AspNetCore.Security.Constraints.api
{
    /// <summary>
    /// Defines an interface for a service that enforces constraints based on authorization decisions.
    /// This service is used to manage and apply different types of constraint handlers in the security layer.
    /// </summary>
    public interface IConstraintEnforcementService
    {
        /// <summary>
        /// Collection of runnable delegate constraint handler providers. These providers offer runnable delegates for enforcing constraints.
        /// </summary>
        [ImportMany] 
        public IEnumerable<IRunnableDelegateConstraintHandlerProvider>? RunnableProviders { get; }

        /// <summary>
        /// Collection of action executing context constraint handler providers. These providers are specialized for handling constraints during the execution of an action.
        /// </summary>
        [ImportMany]
        public IEnumerable<IActionExecutingContextConstraintHandlerProvider>? ActionExecutingContextProviders { get; }

        /// <summary>
        /// Retrieves a bundle of handlers for pre-enforcement constraints based on a given authorization decision.
        /// </summary>
        /// <param name="decision">The authorization decision that dictates the constraints to be enforced.</param>
        /// <returns>A bundle of pre-enforce constraint handlers, if applicable.</returns>
        BlockingPreEnforceConstraintHandlerBundle? BlockingPreEnforceBundleFor(AuthorizationDecision decision);

        /// <summary>
        /// Constructs a post-enforcement constraint handler bundle for a given type and instance, based on an authorization decision.
        /// </summary>
        /// <param name="objectType">The type of object to which the constraints apply.</param>
        /// <param name="instance">The instance of the object, if available.</param>
        /// <param name="decision">The authorization decision influencing the constraints.</param>
        /// <returns>A bundle of post-enforce constraint handlers.</returns>
        public IBlockingPostEnforceConstraintHandlerBundle BlockingPostEnforceConstraintHandlerBundle(Type objectType, object? instance, AuthorizationDecision decision);
    }
}
