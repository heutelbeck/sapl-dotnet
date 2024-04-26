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

using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Authentication.Metadata;

namespace SAPL.AspNetCore.Security.Authentication.Identity
{
    /// <summary>
    /// Responsible for building a JSON representation of the subject from the authenticated user's identity.
    /// This class is used in scenarios where the subject details need to be extracted from the user's claims.
    /// </summary>
    public class SubjectFromIdentityBuilder : ISubjectFromAuthenticationBuilder
    {
        /// <summary>
        /// Extracts the claims of the authenticated user and constructs a JSON object representing the subject.
        /// </summary>
        /// <param name="httpContext">The HTTP context containing the authenticated user information.</param>
        /// <returns>A JToken representing the subject, or null if no relevant claims are found.</returns>
        public JToken? GetSubject(HttpContext httpContext)
        {
            // Dictionary to store user claims
            Dictionary<string, string> claims = new Dictionary<string, string>();

            // Iterate over the user's claims and add them to the dictionary
            if (httpContext.User.Claims.Any())
            {
                foreach (Claim claim in httpContext.User.Claims)
                {
                    claims.Add(claim.ValueType, claim.Value);
                }
            }

            // Serialize the claims to JSON if any exist
            if (claims.Any())
            {
                string json = JsonConvert.SerializeObject(claims, Formatting.Indented);
                if (!string.IsNullOrEmpty(json))
                {
                    // Parse the JSON string into a JToken and return it
                    return JToken.Parse(json);
                }
            }

            // Return null if there are no claims to serialize
            return null;
        }
    }
}