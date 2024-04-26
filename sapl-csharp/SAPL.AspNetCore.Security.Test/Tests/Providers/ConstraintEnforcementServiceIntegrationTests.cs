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
using SAPL.AspNetCore.Security.Test.TestConfiguration;
using Xunit;

namespace SAPL.AspNetCore.Security.Test.Tests.Providers;

public class ConstraintEnforcementServiceIntegrationTests : IClassFixture<WebApplicationFactoryForCEIT<Program>>
{


    //private static JArray ONE_CONSTRAINT = new JArray();
    private static string summaryWihIndex1 = "Bracing";
    //private static string summaryWihIndex4 = "Mild";

    private readonly WebApplicationFactory<Program> _factory;

    public ConstraintEnforcementServiceIntegrationTests(WebApplicationFactoryForCEIT<Program> factory)
    {
        _factory = factory;
    }


    [Theory]
    [InlineData("GetSum/1")]
    public async Task when_parameter_and_no_ActionExcecutingConstraint_then_unmodified_response(string url)
    {
        // Arrange
        var client = _factory.CreateClient();
        // Act
        var response = await client.GetAsync(url);
        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299

        var responseAsync = await response.Content.ReadAsStringAsync();
        Assert.True(responseAsync.Equals(summaryWihIndex1));
    }

    //[Theory]
    //[InlineData("GetSum/1")]
    //public async Task when_parameter_and_ActionExcecutingConstraint_then_modified_response(string url)
    //{

    //    //ActionExcecuting

    //    IActionExecutingContextConstraintHandlerProvider provider = Substitute.For<IActionExecutingContextConstraintHandlerProvider>();
    //    provider.GetSignal().Returns(ISignal.Signal.ON_DECISION);
    //    provider.IsResponsible(ONE_CONSTRAINT.ToList().First()).Returns(true);
    //    provider.Accept().Returns(context =>
    //    {
    //        IDictionary<string, object?> originalArguments = context.ActionArguments;
    //        IDictionary<string, object?> newArguments = new Dictionary<string, object?>(originalArguments);

    //        foreach (KeyValuePair<string, object?> argument in newArguments)
    //        {
    //            if (argument.Value is string value)
    //            {
    //                if (value == "1")
    //                {
    //                    newArguments[argument.Key] = "2";
    //                }
    //            }
    //        }
    //        context.ActionArguments.Clear();
    //        foreach (var (key, value) in newArguments)
    //        {
    //            context.ActionArguments.Add(key, value);
    //        }

    //    });
    //    //provider.Accept().
    //    //provider.When(p => p.Accept()).Do(info => AssertTest(1));

    //    Assert.True(provider.IsResponsible(ONE_CONSTRAINT.ToList().First()));


    //    var decision = AuthorizationDecision.PERMIT.WithObligations(ONE_CONSTRAINT);
    //    var service = new ConstraintEnforcementService(
    //        new List<IResponsibleConstraintHandlerProvider>() { provider });

    //    var exception = Record.Exception(() =>
    //    {

    //        BlockingPreEnforceConstraintHandlerBundle bundle = service.BlockingPreEnforceBundleFor(decision);
    //        bundle.HandleOnDecisionConstraints();
    //    });
    //    Assert.Null(exception);
    //    Assert.True(service.UnhandledObligations == null || !service.UnhandledObligations.Any());
    //    Assert.True(service.RunnableProviders != null && service.RunnableProviders.Count().Equals(1));
    //    //return Task.CompletedTask;


    //    //// Arrange
    //    //var client = _factory.CreateClient();
    //    //// Act
    //    //var response = await client.GetAsync(url);
    //    //// Assert
    //    //response.EnsureSuccessStatusCode(); // Status Code 200-299

    //    //var responseAsync = await response.Content.ReadAsStringAsync();
    //    //Assert.True(responseAsync.Equals(summaryWihIndex4));
    //}

    [Theory]
    [InlineData("GetForecasts")]
    public async Task Get_EndpointsReturnSuccessAndCorrectContentType(string url)
    {
        // Arrange
        var client = _factory.CreateClient();
        // Act
        var response = await client.GetAsync(url);
        // Assert
        response.EnsureSuccessStatusCode(); // Status Code 200-299

        var responseAsync = response.Content.ReadAsStringAsync();

        Assert.Equal("application/json; charset=utf-8",
            response.Content.Headers.ContentType?.ToString());
    }

    public Task DisposeAsync()
    {
        throw new NotImplementedException();
    }
}


[ApiController]
public class WeatherForecastControllerForConstraintEnforcementServiceIntegrationTests : ControllerBase
{
    private static readonly string[] Summaries = new[]
    {
        "Freezing", "Bracing", "Chilly", "Cool", "Mild", "Warm", "Balmy", "Hot", "Sweltering", "Scorching"
    };


    [HttpGet(template: "/GetForecasts", Name = "Get")]
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

    //[PreEnforce("Mama")]
    [HttpGet(template: "/GetSum/{id}", Name = "GetSum")]
    public string GetSum(int id)
    {
        var patient = Summaries[id];
        return patient;
    }
}


public class WebApplicationFactoryForCEIT<TProgram>
    : WebApplicationFactory<TProgram> where TProgram : class
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
            {
                services.AddMvc().PartManager.FeatureProviders.Add(new ControllerTypeResolver(typeof(WeatherForecastControllerForConstraintEnforcementServiceIntegrationTests)));
            }
        );
        builder.UseEnvironment("Development");
    }
}