// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Authentication.Metadata;

namespace SAPL.AspNetCore.Security.Authentication.Jwt.SubjectBuilder
{
    /// <summary>
    /// Extracts subject information from JWT tokens in HTTP requests for authentication purposes.
    /// </summary>
    public class SubjectFromAuthenticationFromJwtTokenBuilder : ISubjectFromAuthenticationBuilder
    {
        // Configuration instance to access application settings
        private readonly IConfiguration? appSettings;
        // Utility class for handling JWT tokens
        private readonly JwtUtil jwtUtil;

        /// <summary>
        /// Initializes a new instance of the SubjectFromAuthenticationFromJwtTokenBuilder class.
        /// </summary>
        /// <param name="configuration">The application's configuration containing settings for JWT.</param>
        public SubjectFromAuthenticationFromJwtTokenBuilder(IConfiguration? configuration)
        {
            appSettings = configuration;
            jwtUtil = new JwtUtil();
        }

        /// <summary>
        /// Retrieves the subject from a JWT token present in the HTTP request header.
        /// </summary>
        /// <param name="httpContext">The current HTTP context containing the JWT token.</param>
        /// <returns>A JToken representing the subject, or null if no valid token is found.</returns>
        public JToken? GetSubject(HttpContext httpContext)
        {
            // Extracting the token from the Authorization header
            var token = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            if (token != null)
            {
                // Decrypting the token to get its payload and converting it to JSON
                var payload = jwtUtil.GetDecryptedJwtToken(token, appSettings!)?.Payload.SerializeToJson();
                if (payload != null) return JToken.Parse(payload);
            }
            // Returning null if no valid token is present
            return null;
        }
    }
}
