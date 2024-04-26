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
    /// Defines an interface for creating runnable delegates. 
    /// This interface is used in security constraints handling, allowing the definition of custom actions that can be executed as part of security processing.
    /// </summary>
    public interface IRunnableDelegate
    {
        /// <summary>
        /// Creates and returns an action that can be run. 
        /// This method is responsible for defining the logic of the action that will be executed.
        /// </summary>
        /// <returns>An Action delegate that encapsulates the executable logic.</returns>
        Action Run();
    }
}