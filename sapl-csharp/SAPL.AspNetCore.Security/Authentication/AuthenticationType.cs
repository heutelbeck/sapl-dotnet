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


namespace SAPL.AspNetCore.Security.Authentication;

/// <summary>
/// Defines the types of authentication that can be used in the application.
/// This enumeration helps in configuring and distinguishing between different
/// authentication mechanisms.
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// Bearer Token authentication: A security token (JWT, for example) is sent
    /// in the Authorization header of the HTTP request. Commonly used in RESTful APIs.
    /// </summary>
    Bearer_Token,

    /// <summary>
    /// Identity authentication: Uses ASP.NET Core Identity for handling user
    /// authentication and authorization. Typically involves user credentials like
    /// username and password.
    /// </summary>
    Identity,

    /// <summary>
    /// Custom authentication: Allows the use of a custom-developed authentication
    /// mechanism tailored to specific requirements not covered by the standard types.
    /// </summary>
    Custom
}