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
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using SAPL.WebAPIDemo.ExampleData.Data;
using SAPL.WebAPIDemo.ExampleData.Models;
using Xunit;

namespace Web_API_Demo.Tests
{
    public class AuthenticationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> webApplication;

        public AuthenticationIntegrationTests(WebApplicationFactory<Program> factory)
        {
            this.webApplication = factory;
        }

        [Theory]
        [InlineData("api/Authenticate/Login")]
        public async Task When_no_username_and_password_then_no_JWTToken(string url)
        {
            // Arrange
            var client = webApplication.CreateClient();

            var wrongUser = new JObject
            {
                { nameof(User.Username), "wrongUser" },
                { nameof(User.Password), "wrongUser" }
            };

            HttpContent payload = new StringContent(wrongUser.ToString(), Encoding.UTF8, "application/json");
            var response = await client.PostAsync(url, payload);
            Assert.True(response.StatusCode == HttpStatusCode.InternalServerError);
        }

        [Theory]
        [InlineData("api/Authenticate/Login")]
        public async Task When_username_and_password_then_JWTToken(string url)
        {
            var userName = "Robert";

            // Arrange
            var client = webApplication.CreateClient();

            var user = EmployeeCollection.GetEmployee(userName);
            var userJObject = new JObject
            {
                { nameof(User.Username), user!.Username },
                { nameof(User.Password), "ApiKey" }
            };

            HttpContent payload = new StringContent(userJObject.ToString(), Encoding.UTF8, "application/json");

            // Act
            var response = await client.PostAsync(url, payload);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(token));
        }
    }
}
