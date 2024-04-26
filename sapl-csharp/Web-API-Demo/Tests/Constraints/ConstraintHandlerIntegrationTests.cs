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

using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json.Linq;
using System.Net.Http.Headers;
using System.Text;
using Newtonsoft.Json;
using SAPL.PDP.Api;
using Xunit;
using SAPL.WebAPIDemo.ExampleData.Models;
using SAPL.WebAPIDemo.ExampleData.Data;

namespace Web_API_Demo.Tests.Constraints
{
#if !EXCLUDE_TESTS
    public class ConstraintHandlerIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
    {
        private readonly WebApplicationFactory<Program> webApplication;
        private readonly HttpClient client;


        public ConstraintHandlerIntegrationTests(WebApplicationFactory<Program> factory)
        {
            this.webApplication = factory;
            client = webApplication.CreateClient();
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetAllPatients")]
        public async Task When_no_SaplAttribute_then_AllPatients(string url)
        {
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();
            var patients = await patientsResponse.Content.ReadAsStringAsync();

            Assert.False(string.IsNullOrEmpty(patients));
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetAllPatientsUser")]
        public async Task When_SaplAttribute_And_Policy_then_AllPatients(string url)
        {
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();

            var patients = await patientsResponse.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(patients));
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetPatients")]
        public async Task When_SaplAttribute_And_Policy_then_GetPatients(string url)
        {
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();

            var patients = await patientsResponse.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(patients));
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetPatientsWithBrokenLeg")]
        public async Task When_Obligation_And_DelegateConstrainthandler_then_Access_permitted(string url)
        {
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();

            var patients = await patientsResponse.Content.ReadAsStringAsync();
            Assert.False(string.IsNullOrEmpty(patients));
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetPatientById/1")]
        public async Task When_Obligation_And_ActionDescriptorConstrainthandlerChangesId_then_PatientId_is_2(string url)
        {
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();

            var content = await patientsResponse.Content.ReadAsStringAsync();
            var patient = JsonConvert.DeserializeObject<Patient>(content);
            Assert.NotNull(patient);
            Assert.True(patient.Id.Equals("2"));
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetAllPatientsUserForTypedFilter")]
        public async Task When_FilterTypedContententObligation_And_Constrainthandler_then_PatientLastNameIsBlubb(string url)
        {
            IEnumerable<Patient> completePatients = null!;
            int counter = 0;
            //while (counter < 3)
            //{
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();
            var content = await patientsResponse.Content.ReadAsStringAsync();
            var patients = JsonConvert.DeserializeObject<IEnumerable<Patient>>(content);
            completePatients = patients!;
            counter++;
            //}
            AuthorizationProvider.Current.EndTransmission();
            Assert.NotNull(completePatients);
            Assert.True(completePatients.All(p => p.LastName.Equals("Blubb")));
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetAllPatientsUserForJsonFilter")]
        public async Task When_JsonFilterAndReplaceLastNameObligation_And_Constrainthandler_then_PatientLastNameIs_not_Blubb(string url)
        {
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();
            var content = await patientsResponse.Content.ReadAsStringAsync();
            var patients = JsonConvert.DeserializeObject<IEnumerable<Patient>>(content);
            Assert.NotNull(patients);
            Assert.True(!patients.Any(p => p.LastName.Equals("Blubb")));
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetAllPatientsUserForJsonFilterBlacken")]
        public async Task When_JsonFilterAndBlackenLastNameObligation_And_Constrainthandler_then_PatientLastNameIs_not_Blubb(string url)
        {
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();
            var content = await patientsResponse.Content.ReadAsStringAsync();
            var patients = JsonConvert.DeserializeObject<IEnumerable<Patient>>(content);
            Assert.NotNull(patients);
            Assert.True(!patients.Any(p => p.LastName.Equals("Blubb")));
        }

        [Trait("Integration", "PDPRequired")]
        [Theory]
        [InlineData("api/Patient/GetAllPatientsUserForJsonFilterDelete")]
        public async Task When_JsonFilterAndDeletePatientObligation_And_Constrainthandler_then_Patient_deleted(string url)
        {
            var bearerToken = await GetValidBearerToken();
            var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
            HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
            patientsResponse.EnsureSuccessStatusCode();
            var content = await patientsResponse.Content.ReadAsStringAsync();
            var patients = JsonConvert.DeserializeObject<IEnumerable<Patient>>(content);
            Assert.NotNull(patients);
            Assert.True(!patients.Any(p => p.LastName.Equals("Blubb")));
        }


        private async Task<string> GetValidBearerToken()
        {
            var userName = "Robert";
            var user = EmployeeCollection.GetEmployee(userName);
            var userJObject = new JObject
            {
                { nameof(User.Username), user!.Username },
                { nameof(User.Password), "Password" }
            };
            HttpContent payload = new StringContent(userJObject.ToString(), Encoding.UTF8, "application/json");

            // Act
            var authenticateUrl = "api/Authenticate/Login";
            var response = await client.PostAsync(authenticateUrl, payload);
            response.EnsureSuccessStatusCode();
            var token = await response.Content.ReadAsStringAsync();
            return token;
        }
    }
}
#endif