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
    /// Determines the time of execution for the constraint handlers
    /// </summary>
    public interface ISignal
    {
        /// <summary>
        /// Enum for the times of execution
        /// </summary>
        enum Signal
        {
            ON_DECISION, ON_EXECUTION
        }

        /// <summary>
        /// Gets the signal.
        /// </summary>
        /// <returns>The time of execution</returns>
        Signal GetSignal();
    }
}
