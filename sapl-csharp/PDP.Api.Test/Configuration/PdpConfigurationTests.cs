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
using NSubstitute;
using SAPL.PDP.Api.Configuration;

namespace SAPL.PDP.Api.Test.Configuration
{
    public class PdpConfigurationTests
    {
        [Trait("Unit", "NoPDPRequired")]
        [Fact]
        public Task When_Custom_Configuaration_Set_Then_PdpCOnfiguration_Available()
        {
            IConfiguration configuration = Substitute.For<IConfiguration>();
            configuration["SAPL:BaseUri"] = "https://localhost:8443";
            configuration["SAPL:Username"] = "YJidgyT2mfdkbmL";
            configuration["SAPL:Password"] = "Fa4zvYQdiwHZVXh";

            PdpConfiguration pdpConfiguration = new PdpConfiguration(configuration);
            Assert.NotNull(pdpConfiguration.BaseUri);
            Assert.NotNull(pdpConfiguration.Username);
            Assert.NotNull(pdpConfiguration.Password);
            Assert.True(pdpConfiguration.IsValid());
            return Task.CompletedTask;
        }
    }
}
