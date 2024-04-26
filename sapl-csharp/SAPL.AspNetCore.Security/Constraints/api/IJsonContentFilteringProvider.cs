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
    /// Defines an interface for a provider that offers handlers specifically for filtering JSON content.
    /// This is part of a security constraints framework and is used to apply specific filtering logic
    /// to JSON-formatted data, which is crucial in managing and enforcing security constraints on such data.
    /// </summary>
    public interface IJsonContentFilteringProvider : IResponsibleConstraintHandlerProvider
    {
        /// <summary>
        /// Retrieves a handler that is capable of processing and filtering JSON content.
        /// This handler applies specific security rules or constraints to JSON data, ensuring that it meets the defined security criteria.
        /// </summary>
        /// <returns>An object representing the handler, which contains logic for filtering JSON content. The actual type and behavior of the handler depend on the specific implementation.</returns>
        object GetHandler();
    }
}