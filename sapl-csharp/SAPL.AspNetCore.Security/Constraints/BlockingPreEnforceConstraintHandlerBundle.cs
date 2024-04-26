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
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;

namespace SAPL.AspNetCore.Security.Constraints
{
    /// <summary>
    /// This bundle aggregates all constraint handlers for a specific decision which
    /// are useful in a blocking PreEnforce scenario.
    /// </summary>
    public class BlockingPreEnforceConstraintHandlerBundle
    {
        private List<IRunnableDelegate>? OnDecisionHandlers { get; }
        private List<IConsumerDelegate<ActionExecutingContext>> ActionExecutingHandlers { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="BlockingPreEnforceConstraintHandlerBundle"/> class.
        /// </summary>
        /// <param name="onDecisionHandlers">The on decision handlers.</param>
        /// <param name="actionExecutingHandlers">The action executing handlers.</param>
        public BlockingPreEnforceConstraintHandlerBundle(List<IRunnableDelegate>? onDecisionHandlers,
            List<IConsumerDelegate<ActionExecutingContext>> actionExecutingHandlers)
        {
            OnDecisionHandlers = onDecisionHandlers;
            ActionExecutingHandlers = actionExecutingHandlers;
        }

        /// <summary>
        /// Handles the on decision constraints.
        /// </summary>
        public void HandleOnDecisionConstraints()
        {
            OnDecisionHandlers?.ForEach(r => r.Run().Invoke());
        }

        /// <summary>
        /// Handles the method invocation handlers.
        /// </summary>
        /// <param name="actionDescriptor">The action descriptor.</param>
        public void HandleMethodInvocationHandlers(ActionExecutingContext actionDescriptor)
        {
            try
            {
                ActionExecutingHandlers.ForEach(a => a.Accept().Invoke(actionDescriptor));
            }
            catch (Exception? e)
            {
                throw new AccessDeniedException(e);
            }

        }

        /// <summary>
        /// Determines whether this instance has action executing handlers.
        /// </summary>
        /// <returns>
        ///   <c>true</c> if this instance has action executing handlers; otherwise, <c>false</c>.
        /// </returns>
        public bool HasActionExecutingHandlers()
        {
            return ActionExecutingHandlers.Any();
        }
    }
}
