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

namespace SAPL.AspNetCore.Security.PolicyMapping
{
    /// <summary>
    /// Represents an obligation within a security policy.
    /// An obligation is a specified action or set of actions that are required to be executed
    /// when certain conditions are met.
    /// </summary>
    public class Obligation
    {
        /// <summary>
        /// The type of obligation. This often categorizes the obligation or indicates
        /// the nature of actions to be taken when the obligation's conditions are met.
        /// </summary>
        public string type = null!;

        /// <summary>
        /// The set of conditions 
        /// Each condition is evaluated, and if all are met, the obligation is enacted.
        /// </summary>
        public Condition[] conditions = null!;
    }
}

