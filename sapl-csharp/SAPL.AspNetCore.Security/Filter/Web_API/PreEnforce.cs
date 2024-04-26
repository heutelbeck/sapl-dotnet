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

using SAPL.AspNetCore.Security.Filter.Base;

namespace SAPL.AspNetCore.Security.Filter.Web_API
{
    /// <summary>
    /// An attribute to enforce SAPL-based security policies before the execution of an action method.
    /// This attribute should be applied to controller actions in ASP.NET Core Web API applications.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
    public class PreEnforce : SaplPreEnforceAttributeBase
    {
        /// <summary>
        /// Initializes a new instance of the PreEnforce attribute with specified SAPL policy attributes.
        /// </summary>
        /// <param name="subject">The subject property for the SAPL policy, typically representing the user or entity performing the action.</param>
        /// <param name="action">The action property for the SAPL policy, indicating what action is being performed.</param>
        /// <param name="resource">The resource property for the SAPL policy, referring to the specific item or data the action is performed on.</param>
        /// <param name="environment">The environment property for the SAPL policy, providing contextual information like time of day, location, etc.</param>
        public PreEnforce(string? subject = null, string? action = null, string? resource = null,
            string? environment = null)
        {
            Subject = subject;
            Action = action;
            Resource = resource;
            Environment = environment;
        }
    }
}
