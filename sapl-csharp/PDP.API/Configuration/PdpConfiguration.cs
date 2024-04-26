
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

#nullable enable
using csharp.sapl.pdp.Configuration.Metadata;
using Microsoft.Extensions.Configuration;

namespace SAPL.PDP.Api.Configuration
{
    public class PdpConfiguration : IPdpConfiguration
    {
        public string BaseUri { get; }
        public string Username { get; }
        public string Password { get; }

        public PdpConfiguration(IConfiguration? applicationSettings)
        {
            BaseUri = applicationSettings?["SAPL:BaseUri"] ?? string.Empty;
            Username = applicationSettings?["SAPL:Username"] ?? string.Empty;
            Password = applicationSettings?["SAPL:Password"] ?? string.Empty;
        }

        public bool IsValid()
        {
            return !string.IsNullOrEmpty(BaseUri) &&
                   !string.IsNullOrEmpty(Username) &&
                   !string.IsNullOrEmpty(Password);
        }
    }
}
