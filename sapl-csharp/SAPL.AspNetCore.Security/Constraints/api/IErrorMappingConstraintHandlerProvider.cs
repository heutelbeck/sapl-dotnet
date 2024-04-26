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
    /// Defines an interface for a provider that offers handlers for mapping one type of exception to another.
    /// This is part of a security constraints framework, where exceptions may need to be transformed or interpreted differently
    /// based on specific constraints, often defined in JSON format.
    /// </summary>
    public interface IErrorMappingConstraintHandlerProvider : IResponsibleConstraintHandlerProvider
    {
        /// <summary>
        /// Retrieves a function that maps an input exception to another type of exception based on a given constraint.
        /// The constraint is typically specified in JSON format (JToken), allowing dynamic definitions of error mapping logic.
        /// </summary>
        /// <param name="constraint">The constraint, expressed as a JToken, which defines how exceptions should be mapped.</param>
        /// <returns>A function that takes an Exception and returns another Exception based on the specified constraint.</returns>
        Func<Exception, Exception> GetHandler(JToken constraint);
    }
}