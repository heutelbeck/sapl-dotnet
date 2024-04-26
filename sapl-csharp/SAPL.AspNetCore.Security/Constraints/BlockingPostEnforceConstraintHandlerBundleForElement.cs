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

using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Exceptions;

namespace SAPL.AspNetCore.Security.Constraints
{
    /// <summary>
    /// This bundle aggregates all constraint handlers for a specific decision which
    /// are useful in a blocking PostEnforce scenario.
    /// For single object types.
    /// </summary>
    /// <typeparam name="TElement">The type of element.</typeparam>
    public class BlockingPostEnforceConstraintHandlerConstraintHandlerBundleForElement<TElement> : BlockingPostEnforceConstraintHandlerBundleBase
                                                                                where TElement : class
    {
        private List<IConsumerDelegate<TElement?>>? ConsumerDelegatesHandlers { get; set; }
        private List<Func<TElement, TElement?>>? JsonContentFilteringAndTransformationHandlers { get; set; }
        public TElement? ActionResultValue { get; set; }

        /// <summary>
        /// Gets the result value.
        /// </summary>
        /// <returns>The result value.</returns>
        public override object? GetResultValue()
        {
            return ActionResultValue;
        }

        /// <summary>
        /// Sets the result value.
        /// </summary>
        /// <param name="value">The value to set.</param>
        public override void SetResultValue(object? value)
        {
            this.ActionResultValue = (TElement)value!;
        }

        /// <summary>
        /// Handles the filter predicate and transformation handlers.
        /// </summary>
        /// <returns>The result value.</returns>
        public override object? HandleFilterPredicateAndTransformationHandlers()
        {
            return ActionResultValue;
        }

        /// <summary>
        /// Handles all constraints.
        /// </summary>
        /// <returns>The result value.</returns>
        public override object? HandleAllConstraints()
        {
            return HandleJsonFilterPredicateAndTransformationHandlers();
        }

        /// <summary>
        /// Handles the JSON filter predicate and transformation handlers.
        /// </summary>
        /// <returns>The result value.</returns>
        public override object? HandleJsonFilterPredicateAndTransformationHandlers()
        {
            return HandleJsonListFilterPredicateAndTransformationHandlers();
        }

        /// <summary>
        /// Handles all consumer delegate constraints.
        /// </summary>
        public override void HandleAllConsumerDelegateConstraints()
        {
            try
            {
                if (ConsumerDelegatesHandlers != null)
                {
                    ConsumerDelegatesHandlers.ForEach(a => a.Accept().Invoke(ActionResultValue));
                }
            }
            catch (Exception? e)
            {
                throw new AccessDeniedException(e);
            }
        }

        /// <summary>
        /// Constructs the handlers.
        /// </summary>
        /// <param name="lambdaFilterPredicateHandlers">The lambda filter predicate handlers.</param>
        /// <param name="filterPredicateHandlers">The filter predicate handlers.</param>
        /// <param name="filterPredicateTransformHandlers">The filter predicate transform handlers.</param>
        /// <param name="allJsonContentFilteringAndTransformationHandlers">All JSON content filtering and transformation handlers.</param>
        public override void ConstructHandlers(List<ITypedPredicateConstraintHandlerProvider> lambdaFilterPredicateHandlers, List<IFilterPredicateConstraintHandlerProvider> filterPredicateHandlers,
            List<ITypedContentFilteringProvider> filterPredicateTransformHandlers, List<IJsonContentFilteringProvider> allJsonContentFilteringAndTransformationHandlers)
        {
            this.JsonContentFilteringAndTransformationHandlers = new List<Func<TElement, TElement>>()!;
            foreach (var jsonContentFilteringAndTransformationHandler in allJsonContentFilteringAndTransformationHandlers)
            {
                if (jsonContentFilteringAndTransformationHandler.GetHandler() is Func<TElement, TElement> objectHandler)
                {
                    this.JsonContentFilteringAndTransformationHandlers.Add(objectHandler);
                }
            }
            this.ConsumerDelegatesHandlers = new List<IConsumerDelegate<TElement>>()!;
        }

        /// <summary>
        /// Handles the JSON list filter predicate and transformation handlers.
        /// </summary>
        /// <returns>The result value.</returns>
        public TElement? HandleJsonListFilterPredicateAndTransformationHandlers()
        {
            if (JsonContentFilteringAndTransformationHandlers != null)
            {
                foreach (var handler in JsonContentFilteringAndTransformationHandlers)
                {
                    if (ActionResultValue != null) ActionResultValue = handler.Invoke(ActionResultValue);
                }
            }

            return ActionResultValue;
        }
    }
}
