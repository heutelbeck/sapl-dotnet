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
namespace SAPL.AspNetCore.Security.Constraints.api;

/// <summary>
/// Defines the interface for handling various post-enforcement constraints in an application. 
/// This includes handling filter predicates, transformation handlers, and JSON-specific processing.
/// </summary>
public interface IBlockingPostEnforceConstraintHandlerBundle
{
    /// <summary>
    /// Handles filter predicates and transformations applied after enforcing constraints.
    /// </summary>
    /// <returns>Potentially transformed or filtered results.</returns>
    object? HandleFilterPredicateAndTransformationHandlers();

    /// <summary>
    /// Processes all constraints defined in the handler bundle.
    /// </summary>
    /// <returns>Potentially transformed or filtered results.</returns>
    object? HandleAllConstraints();

    /// <summary>
    /// Specifically handles JSON filter predicates and transformations.
    /// </summary>
    /// <returns>Potentially transformed or filtered JSON data.</returns>
    object? HandleJsonFilterPredicateAndTransformationHandlers();

    /// <summary>
    /// Handles all error constraints based on the provided exception.
    /// </summary>
    /// <param name="exception">The exception to process.</param>
    /// <returns>An exception after applying error handling logic.</returns>
    Exception? HandleAllOnErrorConstraints(Exception exception);

    /// <summary>
    /// Constructs various handlers for processing constraints, including filter predicates, transformation handlers, 
    /// and error handling logic.
    /// </summary>
    /// <param name="lambdaFilterPredicateHandlers">Handlers for lambda filter predicates.</param>
    /// <param name="filterPredicateHandlers">Handlers for general filter predicates.</param>
    /// <param name="filterPredicateTransformHandlers">Handlers for transforming filter predicates.</param>
    /// <param name="allJsonContentFilteringAndTransformationHandlers">Handlers for JSON content filtering and transformation.</param>
    /// <param name="errorHandlers">Error handling actions.</param>
    /// <param name="errorMappingHandlers">Error mapping functions.</param>
    /// <param name="runnableHandlers">List of runnable delegates for custom processing.</param>
    void ConstructHandlers(List<ITypedPredicateConstraintHandlerProvider> lambdaFilterPredicateHandlers,
        List<IFilterPredicateConstraintHandlerProvider> filterPredicateHandlers,
        List<ITypedContentFilteringProvider> filterPredicateTransformHandlers,
        List<IJsonContentFilteringProvider> allJsonContentFilteringAndTransformationHandlers,
        IEnumerable<Action<Exception>>? errorHandlers,
        IEnumerable<Func<Exception, Exception>>? errorMappingHandlers,
        List<IRunnableDelegate>? runnableHandlers);

    /// <summary>
    /// Gets or sets a value that can be used to hold the result of constraint processing.
    /// </summary>
    object? ResultValue { get; set; }

    /// <summary>
    /// Executes additional processing or decision-making logic after all constraints have been handled.
    /// </summary>
    void HandleOnDecisionConstraints();
}
