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

using SAPL.AspNetCore.Security.Filter.Web_API;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Filter
{
    public class PreEnforceTests
    {
        [Trait("Unit", "NoPDPRequired")]
        [Theory]
        [InlineData("SomeSubject", "SomeAction", "SomeResource", "SomeEnvironment")]
        public void WhenPreEnforceWithCustomValues_then_ValuesAreSet(string subject, string action, string resource, string environment)
        {
            PreEnforce preEnforceFilter = new PreEnforce(subject, action, resource, environment);
            Assert.True(preEnforceFilter.Subject != null && preEnforceFilter.Subject.Equals(subject));
            Assert.True(preEnforceFilter.Action != null && preEnforceFilter.Action.Equals(action));
            Assert.True(preEnforceFilter.Resource != null && preEnforceFilter.Resource.Equals(resource));
            Assert.True(preEnforceFilter.Environment != null && preEnforceFilter.Environment.Equals(environment));
        }
    }
}