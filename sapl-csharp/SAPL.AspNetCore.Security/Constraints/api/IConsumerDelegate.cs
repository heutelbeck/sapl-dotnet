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
    /// Defines an interface for creating a consumer delegate for a specific type.
    /// This delegate is intended for use in processing or handling data of type T within security constraints.
    /// </summary>
    /// <typeparam name="T">The type of data the delegate will consume.</typeparam>
    public interface IConsumerDelegate<in T>
    {
        /// <summary>
        /// Returns an action delegate that can process or handle an object of type T.
        /// </summary>
        /// <returns>An action delegate of type T.</returns>
        Action<T> Accept();
    }
}