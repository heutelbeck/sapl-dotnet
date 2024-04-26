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

using System.Diagnostics;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using SAPL.AspNetCore.Security.Authentication.Jwt.SubjectBuilder;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Authentication
{
    /// <summary>
    /// Contains unit tests for the SubjectFromAuthenticationFromJwtTokenBuilder class, ensuring it correctly creates a subject from a JWT.
    /// </summary>
    public class SubjectFromAuthenticationFromJwtTokenBuilderTetst
    {

        /// <summary>
        /// Verifies that when a JWT token is present in the Authorization header, 
        /// a subject is successfully extracted from it.
        /// </summary>
        [Trait("Unit", "NoPDPRequired")]
        [Fact]
        public Task When_JTokenInAuthorizationHeaderThenSubjectSet()
        {
            string FirstName = "SomeFirstname";
            string LastName = "SomeLastName";

            var jwtUtil = new JwtUtil();

            var claims = new List<Claim>
                {
                    new Claim(nameof(FirstName), FirstName),
                    new Claim(nameof(LastName),LastName)
                };

            IConfiguration configuration = Substitute.For<IConfiguration>();
            configuration["JWT:Secret"] = "ByYM000OLlMQG6VVVp1OH7Xzyr7gHuw1qvUC5dcGt3SNM";
            configuration["JWT:ValidAudience"] = "https://ftk-demo.com";
            configuration["JWT:ValidIssuer"] = "https://ftk-demo.com";

            var token = jwtUtil.GetEncryptedJwtToken(claims, configuration);
            var securityToken = new JwtSecurityTokenHandler().WriteToken(token);


            SubjectFromAuthenticationFromJwtTokenBuilder subjectBuilder =
                new SubjectFromAuthenticationFromJwtTokenBuilder(configuration);

            HttpContext httpContext = Substitute.For<HttpContext>();
            httpContext.Request.Headers["Authorization"] = $"Token {securityToken}";

            var tokenFromHeader = httpContext.Request.Headers["Authorization"].FirstOrDefault()?.Split(" ").Last();
            var decryptedToken = jwtUtil.GetDecryptedJwtToken(tokenFromHeader, configuration);

            var subject = subjectBuilder.GetSubject(httpContext);
            Assert.NotNull(subject);
            Debug.Assert(subject != null, nameof(subject) + " != null");
            Assert.True(subject != null && subject.Value<string>(nameof(FirstName))!.Equals(FirstName));
            Assert.True(subject != null && subject.Value<string>(nameof(LastName))!.Equals(LastName));
            return Task.CompletedTask;
        }

        /// <summary>
        /// Tests that when there is no JWT token in the Authorization header,
        /// no subject is extracted (subject is null).
        /// </summary>
        [Trait("Unit", "NoPDPRequired")]
        [Fact]
        public Task When_No_JTokenInAuthorizationHeaderThenSubjectSet()
        {
            string FirstName = "SomeFirstname";
            string LastName = "SomeLastName";

            var claims = new List<Claim>
            {
                new Claim(nameof(FirstName), FirstName),
                new Claim(nameof(LastName),LastName)
            };



            IConfiguration configuration = Substitute.For<IConfiguration>();
            configuration["JWT:Secret"] = "ByYM000OLlMQG6VVVp1OH7Xzyr7gHuw1qvUC5dcGt3SNM";
            configuration["JWT:ValidAudience"] = "https://ftk-demo.com";
            configuration["JWT:ValidIssuer"] = "https://ftk-demo.com";

            SubjectFromAuthenticationFromJwtTokenBuilder subjectBuilder =
                new SubjectFromAuthenticationFromJwtTokenBuilder(configuration);

            HttpContext httpContext = Substitute.For<HttpContext>();

            var subject = subjectBuilder.GetSubject(httpContext);
            Assert.Null(subject);
            return Task.CompletedTask;
        }
    }
}
