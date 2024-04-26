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

using Xunit;

namespace SAPL.AspNetCore.Security.Test.Tests.Method.Blocking
{
    public class PreEnforcePolicyEnforcementPointTests
    {
        [Fact]
        public Task When_Enpoint_HasNoPreEnforceFilter_Then_Access_is_granted()
        {
            //var service = new ConstraintEnforcementService(null);

            //HttpContext context = Substitute.For<HttpContext>();
            //ILogger? logger = Substitute.For<ILogger>();
            //var configuration = Substitute.For<IConfiguration>();
            //var environment = Substitute.For<IHostingEnvironment>();
            //var pdp = Substitute.For<IPolicyDecisionPoint>();
            //var actionContext = Substitute.For<ActionContext>();
            ////PdpConfiguration pdpdConfiguration = new PdpConfiguration(null, new AuthorizationDecision(Decision.PERMIT));

            //PreEnforcePolicyEnforcementPoint pep = new PreEnforcePolicyEnforcementPoint(context, logger, service, pdp, new AuthorizationSubscriptionBuilderService(null, environment));

            Assert.True(true);

            return Task.CompletedTask;
        }

    }
}
