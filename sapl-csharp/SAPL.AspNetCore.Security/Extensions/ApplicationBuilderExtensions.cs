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

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using SAPL.AspNetCore.Security.Authentication;
using SAPL.AspNetCore.Security.Authentication.Identity;
using SAPL.AspNetCore.Security.Authentication.Jwt.SubjectBuilder;
using SAPL.AspNetCore.Security.Authentication.Metadata;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.PDP.Api;
using SAPL.PDP.Client;

namespace SAPL.AspNetCore.Security.Extensions
{
    /// <summary>
    /// Provides extension methods for configuring SAPL integration in an ASP.NET Core application.
    /// </summary>
    public static class ApplicationBuilderExtensions
    {
        /// <summary>
        /// Adds and configures default SAPL services to the service collection.
        /// </summary>
        /// <param name="services">The IServiceCollection to add services to.</param>
        /// <param name="configuration">Application configuration settings.</param>
        /// <param name="authenticationType">The type of authentication to use.</param>
        /// <param name="subjectFromAuthenticationBuilder">Custom builder for deriving subject information from the authentication process. Optional.</param>
        public static void AddDefaultSaplServices(this IServiceCollection services, IConfiguration? configuration, AuthenticationType authenticationType, ISubjectFromAuthenticationBuilder? subjectFromAuthenticationBuilder = null)
        {
            // Add the configuration to the service collection
            services.AddSingleton(x => configuration!);

            // Add a custom subject-from-authentication builder or default builders based on the specified authentication type
            if (subjectFromAuthenticationBuilder != null)
            {
                services.AddSingleton(x => subjectFromAuthenticationBuilder);
            }
            else if (authenticationType == AuthenticationType.Bearer_Token)
            {
                services.AddSingleton<ISubjectFromAuthenticationBuilder>(x => new SubjectFromAuthenticationFromJwtTokenBuilder(configuration));
            }
            else if (authenticationType == AuthenticationType.Identity)
            {
                services.AddSingleton<ISubjectFromAuthenticationBuilder>(x => new SubjectFromIdentityBuilder());
            }

            // Add policy decision point and authorization subscription builder service to the service collection
            services.AddSingleton<IPolicyDecisionPoint>(x => new PolicyDecisionPoint(configuration));
            services.AddSingleton<IAuthorizationSubscriptionBuilderService, AuthorizationSubscriptionBuilderService>();
        }
    }
}

