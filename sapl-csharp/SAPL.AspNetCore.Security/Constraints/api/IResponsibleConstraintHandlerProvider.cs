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
    /// Defines an interface for determining whether a given constraint handler provider is responsible for a particular constraint.
    /// This is part of a security constraints framework, typically used to ensure that the right handler is applied to a specific type of constraint.
    /// </summary>
    public interface IResponsibleConstraintHandlerProvider : ISignal
    {
        /// <summary>
        /// Determines whether this provider is responsible for handling the specified constraint.
        /// </summary>
        /// <param name="constraint">The constraint, often expressed as a JToken, for which the responsibility is being determined.</param>
        /// <returns>
        ///   <c>true</c> if this provider is responsible for the specified constraint; otherwise, <c>false</c>.
        /// </returns>
        bool IsResponsible(JToken constraint);
    }
}