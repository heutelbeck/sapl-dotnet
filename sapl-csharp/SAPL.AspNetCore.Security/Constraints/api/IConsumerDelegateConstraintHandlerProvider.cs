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
    /// Defines an interface for a provider that offers consumer delegates to handle constraints for actions with a specific type parameter.
    /// These handlers are used in security constraints processing, particularly where the type of data being processed is critical.
    /// </summary>
    /// <typeparam name="T">The type of the parameter expected by the consumer delegate.</typeparam>
    public interface IConsumerDelegateConstraintHandlerProvider<T> : IResponsibleConstraintHandlerProvider, IConsumerDelegate<T>
    {
        /// <summary>
        /// Retrieves a consumer delegate that is capable of handling a given constraint.
        /// The constraint is typically specified in a JSON format (JToken), allowing for dynamic and flexible constraint definitions.
        /// </summary>
        /// <param name="constraint">The constraint, expressed as a JToken, to be processed by the handler.</param>
        /// <returns>A consumer delegate capable of processing the specified constraint for the type T.</returns>
        IConsumerDelegate<T> GetHandler(JToken constraint);
    }
}