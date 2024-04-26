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

using Newtonsoft.Json.Linq;

namespace SAPL.AspNetCore.Security.Constraints.api
{
    /// <summary>
    /// Defines an interface for a provider that offers handlers specifically for handling exceptions.
    /// These handlers are part of the security constraints framework and are used to process
    /// specific types of exceptions based on defined constraints.
    /// </summary>
    public interface IErrorHandlerProvider : IResponsibleConstraintHandlerProvider
    {
        /// <summary>
        /// Retrieves a handler that is capable of processing a given constraint that results in an exception.
        /// The constraint is typically specified in a JSON format (JToken), allowing for dynamic and flexible error handling definitions.
        /// </summary>
        /// <param name="constraint">The constraint, expressed as a JToken, which the handler will process in case of an exception.</param>
        /// <returns>An action delegate that handles exceptions according to the specified constraint.</returns>
        Action<Exception> GetHandler(JToken constraint);
    }
}