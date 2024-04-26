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

using System.Diagnostics;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints.api;

namespace Web_API_Demo.Constraints
{
    /// <summary>
    /// A constraint handler that provides logging functionality.
    /// It logs a message when a specific constraint is met.
    /// </summary>
    public class LoggingConsumerDelegateConstraintHandler : IConsumerDelegateConstraintHandlerProvider<string>
    {
        /// <summary>
        /// Returns the handler for the provided constraint.
        /// </summary>
        /// <param name="constraint">The JSON token representing the constraint.</param>
        /// <returns>The consumer delegate handler.</returns>
        public IConsumerDelegate<string> GetHandler(JToken constraint)
        {
            return this;
        }

        /// <summary>
        /// Indicates the signal on which this handler should be triggered.
        /// </summary>
        /// <returns>The signal at which this handler operates.</returns>
        public ISignal.Signal GetSignal()
        {
            return ISignal.Signal.ON_DECISION;
        }

        /// <summary>
        /// Determines if this handler is responsible for the provided constraint.
        /// </summary>
        /// <param name="constraint">The JSON token representing the constraint.</param>
        /// <returns>True if this handler is responsible, false otherwise.</returns>
        public bool IsResponsible(JToken constraint)
        {
            return constraint.Value<string>()!.Equals("logging:inform_admin");
        }

        /// <summary>
        /// Returns the action delegate that will be executed.
        /// </summary>
        /// <returns>The action delegate.</returns>
        public Action<string> Accept()
        {
            return Log;
        }

        /// <summary>
        /// Logs the provided message.
        /// </summary>
        /// <param name="message">The message to be logged.</param>
        public void Log(string message)
        {
            // This method logs the provided message to the debug output.
            Debug.WriteLine(message);
        }
    }
}
