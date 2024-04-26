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

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using SAPL.AspNetCore.Security.Filter.Web_API;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.AspNetCore.Security.Test.TestConfiguration;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Tests.Filter
{
    public class PreEnforceFilterIntegrationTest : IClassFixture<WebApplicationFactoryForPEFTest<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public PreEnforceFilterIntegrationTest(WebApplicationFactoryForPEFTest<Program> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/PrenforceTestAtrributeRoute")]
        public async Task When_Enpoint_Has_SaplAttribute_Then_IsSaplSecure(string url)
        {
            // Arrange
            var context = await _factory.Server.SendAsync(c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = url;
                c.Request.QueryString = new QueryString("?and=query");
            });
            var isSaplSecured = AuthorizationSubscriptionUtil.IsSaplSecure(context);
            Assert.True(isSaplSecured);
        }


        [Theory]
        [InlineData("/PrenforceTestAtrributeRoute")]
        public async Task When_Enpoint_Has_SaplAttribute_And_SubjectI_Set_then_Subject_Not_Null(string url)
        {
            // Arrange
            var context = await _factory.Server.SendAsync(c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = url;
                c.Request.QueryString = new QueryString("?and=query");
            });
            var attribute = AuthorizationSubscriptionUtil.GetSaplAttribute(context);
            Assert.True(!string.IsNullOrEmpty(attribute?.Subject));
        }

        [Theory]
        [InlineData("/PrenforceTestAtrributeRoute")]
        public async Task When_Enpoint_Has_SaplAttribute_And_Resource_Set_Then_Resource_Not_Null(string url)
        {
            // Arrange
            var context = await _factory.Server.SendAsync(c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = url;
                c.Request.QueryString = new QueryString("?and=query");
            });
            var attribute = AuthorizationSubscriptionUtil.GetSaplAttribute(context);
            Assert.True(!string.IsNullOrEmpty(attribute?.Resource));
        }

        [Theory]
        [InlineData("/PrenforceTestAtrributeRoute")]
        public async Task When_Enpoint_Has_SaplAttribute_And_Action_Set_Then_Action_Not_Null(string url)
        {
            // Arrange
            var context = await _factory.Server.SendAsync(c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = url;
                c.Request.QueryString = new QueryString("?and=query");
            });
            var attribute = AuthorizationSubscriptionUtil.GetSaplAttribute(context);
            Assert.True(!string.IsNullOrEmpty(attribute?.Action));
        }

        [Theory]
        [InlineData("/PrenforceTestAtrributeRoute")]
        public async Task When_Enpoint_Has_SaplAttribute_And_Environment_Set_Then_Environment_Not_Null(string url)
        {
            // Arrange
            var context = await _factory.Server.SendAsync(c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = url;
                c.Request.QueryString = new QueryString("?and=query");
            });
            var attribute = AuthorizationSubscriptionUtil.GetSaplAttribute(context);
            Assert.True(!string.IsNullOrEmpty(attribute?.Environment));
        }

    }

    [ApiController]
    [Route("PrenforceTestAtrributeRoute")]
    public class WeatherForecastControllerWithPreEnforceAttribute : ControllerBase
    {
        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [PreEnforce(Subject = "Subject", Action = "Action", Resource = "Resource", Environment = "Environment")]
        [Route("PrenforceTestAtrributeRoute")]
        [HttpGet]
        [HttpGet(Name = "Get1")]
        public IEnumerable<WeatherForecast> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
                .ToArray();
        }
    }

    public class WebApplicationFactoryForPEFTest<TProgram>
        : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
                {
                    services.AddMvc().PartManager.FeatureProviders.Add(new ControllerTypeResolver(typeof(WeatherForecastControllerWithPreEnforceAttribute)));
                }
            );
            builder.UseEnvironment("Development");
        }
    }
}
