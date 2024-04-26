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

using Microsoft.AspNetCore.Http;
using Newtonsoft.Json;
using SAPL.AspNetCore.Security.Constraints.Exceptions;

namespace SAPL.AspNetCore.Security.Middleware.Exception
{
    /// <summary>
    /// Middleware to handle AccessDeniedException in an ASP.NET Core application.
    /// Catches these exceptions and converts them into a structured JSON response.
    /// </summary>
    public class AccessDeniedExceptionMiddleware
    {
        private readonly RequestDelegate _next;

        /// <summary>
        /// Initializes a new instance of the AccessDeniedExceptionMiddleware class.
        /// </summary>
        /// <param name="next">The next middleware in the request pipeline.</param>
        public AccessDeniedExceptionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        /// <summary>
        /// Invokes the middleware to process an HTTP request.
        /// </summary>
        /// <param name="context">The HttpContext for the current request.</param>
        public async Task Invoke(HttpContext context)
        {
            try
            {
                // Proceed with the next middleware
                await _next(context);
            }
            catch (AccessDeniedException exception)
            {
                // Handle the AccessDeniedException and create a JSON response
                var response = context.Response;
                response.ContentType = "application/json";
                response.StatusCode = StatusCodes.Status401Unauthorized;
                var message = JsonConvert.SerializeObject(exception.DetailMessage);
                await response.WriteAsync(message);
            }
        }
    }
}
