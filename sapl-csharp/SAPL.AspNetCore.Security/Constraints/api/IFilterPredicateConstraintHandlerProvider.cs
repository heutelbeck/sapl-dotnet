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

namespace SAPL.AspNetCore.Security.Constraints.api
{
    /// <summary>
    /// Defines an interface for a provider that offers filter predicate handlers.
    /// These handlers are used in the context of security constraints, particularly for filtering data or requests based on certain conditions.
    /// </summary>
    public interface IFilterPredicateConstraintHandlerProvider : IResponsibleConstraintHandlerProvider
    {
        /// <summary>
        /// Retrieves a filter predicate. This predicate is used to filter or evaluate data or requests in the context of security constraints.
        /// The exact nature of the predicate depends on the implementation and the specific security constraints being applied.
        /// </summary>
        /// <returns>An object representing the filter predicate. The actual type and behavior of the predicate depend on the specific implementation.</returns>
        object GetPredicate();
    }
}