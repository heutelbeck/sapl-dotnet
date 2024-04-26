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
    /// Represents a condition in a security policy.
    /// Each condition specifies a rule that must be met for the policy to apply.
    /// </summary>
    public class Condition
    {
        /// <summary>
        /// The path in the data structure to which this condition applies.
        /// This could be a reference to a specific property or field in an object or data model.
        /// </summary>
        public string path = null!;

        /// <summary>
        /// The type of comparison or operation to perform in this condition.
        /// For example, 'replace', 'delete', etc.
        /// </summary>
        public string type = null!;

        /// <summary>
        /// The value to be compared or evaluated in the condition.
        /// </summary>
        public string value = null!;

        /// <summary>
        /// An array of actions or transformations to apply when this condition is met.
        /// These actions could modify data, trigger events, or perform other operations.
        /// </summary>
        public Transformation[] actions = null!;
    }
}

