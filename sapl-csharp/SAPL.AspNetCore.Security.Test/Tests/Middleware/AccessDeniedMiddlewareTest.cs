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

using System.Net;
using Microsoft.AspNetCore.TestHost;
using SAPL.AspNetCore.Security.Constraints.Exceptions;
using SAPL.AspNetCore.Security.Middleware.Exception;
using Xunit;
using static SAPL.AspNetCore.Security.Test.Tests.Middleware.AccessDeniedMiddlewareTest;

namespace SAPL.AspNetCore.Security.Test.Tests.Middleware;

public class AccessDeniedMiddlewareTest
{
    public static class TestConstants
    {
        public static string AccessDeniedExceptionWasThrown = "AccessDeniedException was thrown";
        public static Exception AccessDeniedInnerException = new("AccessDeniedException with innerexception was thrown");
        public static AccessDeniedException DefaultException = new();
    }

    [Fact]
    public async Task AccessDeniedExceptionMiddleware_catches_AccessDeniedExceptionWithDetails_And_Returns_Unauthorized_Response()
    {
        // arrange
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.Configure(applicationBuilder =>
                {
                    applicationBuilder.UseMiddleware<AccessDeniedExceptionMiddleware>();
                    applicationBuilder.UseMiddleware<ThrowAccessDeniedExceptionWithDetailsMiddleware>();
                });
                webHostBuilder.UseTestServer();
            });

        var testHost = await hostBuilder.StartAsync();
        var client = testHost.GetTestClient();
        var response = await client.GetAsync("/test");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(body);
        Assert.True(body.Contains(TestConstants.AccessDeniedExceptionWasThrown));
    }

    [Fact]
    public async Task AccessDeniedExceptionMiddleware_catches_AccessDeniedExceptionWithoutDetails_And_Returns_Unauthorized_Response()
    {
        // arrange
        var hostBuilder = new HostBuilder()
            .ConfigureWebHost(webHostBuilder =>
            {
                webHostBuilder.Configure(applicationBuilder =>
                {
                    applicationBuilder.UseMiddleware<AccessDeniedExceptionMiddleware>();
                    applicationBuilder.UseMiddleware<ThrowAccessDeniedExceptionWithoutDetailsMiddleware>();
                });
                webHostBuilder.UseTestServer();
            });

        var testHost = await hostBuilder.StartAsync();
        var client = testHost.GetTestClient();
        var response = await client.GetAsync("/test");
        Assert.True(response.StatusCode == HttpStatusCode.Unauthorized);
        var body = await response.Content.ReadAsStringAsync();
        Assert.NotEmpty(body);
        Assert.True(body.Contains(TestConstants.DefaultException.DefaultMessage));
    }
}
public class ThrowAccessDeniedExceptionWithDetailsMiddleware
{
    private readonly RequestDelegate _next;
    public ThrowAccessDeniedExceptionWithDetailsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        //await _next(context);
        throw new AccessDeniedException(TestConstants.AccessDeniedExceptionWasThrown);
    }
}

public class ThrowAccessDeniedExceptionWithoutDetailsMiddleware
{
    private readonly RequestDelegate _next;
    public ThrowAccessDeniedExceptionWithoutDetailsMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public Task Invoke(HttpContext context)
    {
        //await _next(context);
        throw new AccessDeniedException();
    }
}
