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
    /// Defines an interface for a provider that offers typed content filtering mechanisms.
    /// This interface is part of a security constraints framework, focusing on filtering data
    /// based on specific types and predefined rules or conditions.
    /// </summary>
    public interface ITypedContentFilteringProvider : IResponsibleConstraintHandlerProvider
    {
        /// <summary>
        /// Retrieves a set of predicates and actions for filtering content based on type.
        /// This method is designed to provide filtering logic tailored to specific data types, 
        /// allowing for more refined and targeted content filtering in security constraint processing.
        /// </summary>
        /// <returns>An object that encapsulates both the predicates for content filtering and the actions to be performed based on those predicates.</returns>
        object GetPredicatesAndActions();
    }
}