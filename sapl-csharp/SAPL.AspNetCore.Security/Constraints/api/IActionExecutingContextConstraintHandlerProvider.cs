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

using Microsoft.AspNetCore.Mvc.Filters;
using Newtonsoft.Json.Linq;

namespace SAPL.AspNetCore.Security.Constraints.api
{
    /// <summary>
    /// Defines an interface for providing handlers that process constraints during the action executing context.
    /// This interface is part of the security constraints framework and extends both IResponsibleConstraintHandlerProvider 
    /// and IConsumerDelegate for ActionExecutingContext.
    /// </summary>
    public interface IActionExecutingContextConstraintHandlerProvider : IResponsibleConstraintHandlerProvider, IConsumerDelegate<ActionExecutingContext>
    {
        /// <summary>
        /// Retrieves a handler that processes a given constraint during an action's execution.
        /// The constraint is provided in JSON format (JToken).
        /// </summary>
        /// <param name="constraint">The constraint to be processed, expressed as a JToken.</param>
        /// <returns>An instance of IConsumerDelegate for ActionExecutingContext that can handle the given constraint.</returns>
        IConsumerDelegate<ActionExecutingContext> GetHandler(JToken constraint);
    }
}