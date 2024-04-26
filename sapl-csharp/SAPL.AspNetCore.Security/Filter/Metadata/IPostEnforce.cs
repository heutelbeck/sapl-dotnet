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

using Microsoft.AspNetCore.Mvc.Filters;

namespace SAPL.AspNetCore.Security.Filter.Metadata
{
    /// <summary>
    /// Defines the contract for an attribute that enforces security policies after an action has executed,
    /// and before the result is processed.
    /// It is part of SAPL (Simple Attribute-based Policy Language) authorization implementation.
    /// </summary>
    public interface IPostEnforce : ISaplAttribute, IAsyncResultFilter
    {
        /// <summary>
        /// Gets the value of the action result which can be inspected or modified by the post-enforcement logic.
        /// </summary>
        public object? ActionResultValue { get; }

        /// <summary>
        /// Gets the type of the action result, allowing the post-enforcement logic to identify what kind of result it's dealing with.
        /// </summary>
        public Type? ActionResultType { get; }
    }
}
