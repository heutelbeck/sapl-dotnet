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

using NSubstitute;
using SAPL.PDP.Api.Configuration;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Tests.Configuration
{
    public class PdpConfigurationTests
    {

        public string BaseUriParameter = "SAPL:BaseUri";
        public string UsernameParameter = "SAPL:Username";
        public string PasswordParameter = "SAPL:Password";

        [Fact]
        public Task When_Configuration_Uncomplete_then_Is_not_Valid()
        {
            IConfiguration? appConfiguration = Substitute.For<IConfiguration>();
            appConfiguration[BaseUriParameter].Returns("www.pdp.com");
            appConfiguration[UsernameParameter].Returns("UserName");

            PdpConfiguration configuration = new PdpConfiguration(appConfiguration);
            Assert.NotNull(configuration.Password);
            Assert.False(configuration.IsValid());
            return Task.CompletedTask;
        }

        [Fact]
        public Task When_Configuration_complete_then_IsValid()
        {
            IConfiguration? appConfiguration = Substitute.For<IConfiguration>();
            appConfiguration[BaseUriParameter].Returns("www.pdp.com");
            appConfiguration[UsernameParameter].Returns("UserName");
            appConfiguration[PasswordParameter].Returns("Password");

            PdpConfiguration configuration = new PdpConfiguration(appConfiguration);
            Assert.True(configuration.IsValid());
            return Task.CompletedTask;
        }
    }
}
