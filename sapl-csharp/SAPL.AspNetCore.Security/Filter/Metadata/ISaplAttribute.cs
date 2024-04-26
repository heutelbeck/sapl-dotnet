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

namespace SAPL.AspNetCore.Security.Filter.Metadata;

/// <summary>
/// Represents a contract for SAPL-based attributes in ASP.NET Core applications.
/// It defines the necessary properties for expressing security policy elements
/// in the context of Simple Attribute-based Policy Language (SAPL).
/// </summary>
public interface ISaplAttribute
{
    /// <summary>
    /// Gets or sets the subject property for the SAPL policy. This typically represents the user or entity performing the action.
    /// </summary>
    public string? Subject { get; set; }

    /// <summary>
    /// Gets or sets the action property for the SAPL policy. This usually indicates what action is being performed, like read, write, delete, etc.
    /// </summary>
    public string? Action { get; set; }

    /// <summary>
    /// Gets or sets the resource property for the SAPL policy. This often refers to the specific item or data that the action is being performed on.
    /// </summary>
    public string? Resource { get; set; }

    /// <summary>
    /// Gets the environment property for the SAPL policy. This can provide context such as the time of day, location, or device type, affecting the decision process.
    /// </summary>
    public string? Environment { get; }
}
