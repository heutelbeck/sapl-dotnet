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
using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Constraints.Providers;
using SAPL.AspNetCore.Security.Filter.Metadata;
using SAPL.AspNetCore.Security.Filter.Web_API;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.AspNetCore.Security.Test.TestConfiguration;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Tests.Filter
{
    public class PostEnforceFilterIntegrationTest : IClassFixture<WebApplicationFactoryForPostEFTest<Program>>
    {
        private readonly WebApplicationFactory<Program> _factory;

        public PostEnforceFilterIntegrationTest(WebApplicationFactoryForPostEFTest<Program> factory)
        {
            _factory = factory;
        }

        [Theory]
        [InlineData("/PostforceTestAtrributeRoute/GetIActionResult")]
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
        [InlineData("/PostforceTestAtrributeRoute/GetIActionResult")]
        public async Task When_Enpoint_Has_SaplAttribute_And_Subject_Set_Then_Subject_Not_Null(string url)
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
        [InlineData("/PostforceTestAtrributeRoute/GetIActionResult")]
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
        [InlineData("/PostforceTestAtrributeRoute/GetIActionResult")]
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

        [Theory]
        [InlineData("/PostforceTestAtrributeRoute/ActionResult")]
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
        [InlineData("/PostforceTestAtrributeRoute/Primitive")]
        public async Task When_enpoint_has_SaplAttribute_and_returnvalue_is_not_IActionResult_then_returnValue_and_type_can_be_identified(string url)
        {
            // Arrange
            var context = await _factory.Server.SendAsync(c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = url;
                c.Request.QueryString = new QueryString("?and=query");
            });
            var attribute = AuthorizationSubscriptionUtil.GetSaplAttribute(context);
            if (attribute is IPostEnforce postEnforce)
            {
                Assert.True(postEnforce.ActionResultType == typeof(IEnumerable<WeatherForecast>));
                var values = postEnforce.ActionResultValue as IEnumerable<WeatherForecast>;
                Assert.NotNull(values);
            }
            else
            {
                Assert.Fail("no ReturnValue found");
            }
        }

        [Theory]
        [InlineData("/PostforceTestAtrributeRoute/ActionResult")]
        public async Task When_enpoint_has_SaplAttribute_and_returnvalue_is_ActionResult_then_returnValue_and_type_can_be_identified(string url)
        {
            // Arrange
            var context = await _factory.Server.SendAsync(c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = url;
                c.Request.QueryString = new QueryString("?and=query");
            });
            var attribute = AuthorizationSubscriptionUtil.GetSaplAttribute(context);
            if (attribute is IPostEnforce postEnforce)
            {
                Assert.True(postEnforce.ActionResultType == typeof(IEnumerable<WeatherForecast>));
                var values = postEnforce.ActionResultValue as IEnumerable<WeatherForecast>;
                Assert.NotNull(values);
            }
            else
            {
                Assert.Fail("no ReturnValue found");
            }
        }

        [Theory]
        [InlineData("/PostforceTestAtrributeRoute/GetIActionResult")]
        public async Task When_enpoint_has_SaplAttribute_and_returnvalue_is_IActionResult_then_returnValue_and_type_can_be_identified(string url)
        {
            // Arrange
            var context = await _factory.Server.SendAsync(c =>
            {
                c.Request.Method = HttpMethods.Get;
                c.Request.Path = url;
                c.Request.QueryString = new QueryString("?and=query");
            });
            var attribute = AuthorizationSubscriptionUtil.GetSaplAttribute(context);
            if (attribute is IPostEnforce postEnforce)
            {
                Assert.True(postEnforce.ActionResultType == typeof(List<WeatherForecast>));
                var values = postEnforce.ActionResultValue as List<WeatherForecast>;
                Assert.NotNull(values);
            }
            else
            {
                Assert.Fail("no ReturnValue found");
            }
        }

        [Theory]
        [InlineData("/PostforceTestAtrributeRoute/ActionResult")]
        public async Task when_client_and_endpoint_PostEnforce_then_Response(string url)
        {
            // Arrange
            var client = _factory.CreateClient();
            // Act
            var response = await client.GetAsync(url);
            // Assert
            response.EnsureSuccessStatusCode(); // Status Code 200-299

            var responseAsync = await response.Content.ReadAsStringAsync();
            Assert.NotNull(responseAsync);
        }
    }

    [ApiController]
    [Route("PostforceTestAtrributeRoute")]
    //[Route("PostforceTestAtrributeRouteResult")]
    public class WeatherForecastControllerWithPostEnforceAttribute : ControllerBase
    {

        private static readonly string[] Summaries = new[]
        {
            "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
        };

        [PostEnforce(Subject = "Subject", Action = "Action", Resource = "Resource", Environment = "Environment")]
        [Route("Primitive")]
        [HttpGet]
        [HttpGet(Name = "GetForecasts")]
        public IEnumerable<WeatherForecast> GetForecasts()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
                .ToArray();
        }


        [PostEnforce(Subject = "Subject", Action = "Action", Resource = "Resource", Environment = "Environment")]
        [Route("ActionResult")]
        [HttpGet]
        [HttpGet(Name = "Get1")]
        public ActionResult<IEnumerable<WeatherForecast>> Get()
        {
            return Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
                .ToArray();
        }

        [PostEnforce(Subject = "Subject", Action = "Action", Resource = "Resource", Environment = "Environment")]
        [Route("GetIActionResult")]
        [HttpGet]
        [HttpGet(Name = "GetIActionResult")]
        [ProducesResponseType(StatusCodes.Status200OK, Type = typeof(IEnumerable<WeatherForecast>))]
        public IActionResult GetIActionResult()
        {
            var forecasts = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            }).ToList();
            return Ok(forecasts);
        }


        [PostEnforce(Subject = "Subject", Action = "Action", Resource = "Resource", Environment = "Environment")]
        [Route("IResult")]
        [HttpGet]
        [HttpGet(Name = "Get2")]
        public IResult GetResult()
        {
            var value = Enumerable.Range(1, 5).Select(index => new WeatherForecast
            {
                Date = DateOnly.FromDateTime(DateTime.Now.AddDays(index)),
                TemperatureC = Random.Shared.Next(-20, 55),
                Summary = Summaries[Random.Shared.Next(Summaries.Length)]
            })
                .ToArray();
            return Results.Ok(value);
        }
    }

    public class WebApplicationFactoryForPostEFTest<TProgram>
        : WebApplicationFactory<TProgram> where TProgram : class
    {
        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureServices(services =>
                {
                    services.AddMvc().PartManager.FeatureProviders.Add(new ControllerTypeResolver(typeof(WeatherForecastControllerWithPostEnforceAttribute)));
                    services.AddTransient<IResponsibleConstraintHandlerProvider, WeatherForecastConstraintHandler>();
                }
            );
            builder.UseEnvironment("Development");
        }
    }

    public class WeatherForecastConstraintHandler : TypedContentFilteringProviderBase<WeatherForecast>
    {
        private const string CONSTRAINT_TYPE = "filterTypedContentWeather";
        protected override bool IsResponsible(JToken constraint)
        {
            var obligationType = ObligationContentReaderUtil.GetObligationType(constraint);
            if (obligationType != null)
            {
                if (obligationType.Equals(CONSTRAINT_TYPE))
                {
                    this.constraint = constraint;
                    return true;
                }
            }
            return false;
        }

        protected override List<(Predicate<WeatherForecast> predicate, List<(Action<WeatherForecast> action, string actionType)> actions)> GetHandlerWithTransformation(JToken constraint)
        {
            var predicates = new List<(Predicate<WeatherForecast> predicate, List<(Action<WeatherForecast> action, string actionType)> actions)>();

            var actions = GetEmptyActions<WeatherForecast>();
            actions.Add(new(GetAction(), "delete"));
            predicates.Add((GetRegExPredicate(), actions));
            return predicates;
        }

        protected Predicate<WeatherForecast> GetRegExPredicate()
        {
            return p => p.TemperatureC > 30;
        }

        public Action<WeatherForecast> GetAction()
        {
            return p => p.Summary = "Hallöle";
        }
    }
}
