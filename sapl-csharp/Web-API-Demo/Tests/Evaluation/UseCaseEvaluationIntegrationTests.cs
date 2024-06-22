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

using System.Net.Http.Headers;
using System.Text;
using Microsoft.AspNetCore.Mvc.Testing;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SAPL.WebAPIDemo.ExampleData.Data;
using SAPL.WebAPIDemo.ExampleData.Models;
using Web_API_Demo.Controllers;
using Xunit;

namespace Web_API_Demo.Tests.Evaluation;

#if !EXCLUDE_TESTS
public class UseCaseEvaluationIntegrationTests : IClassFixture<WebApplicationFactory<Program>>
{
    private readonly WebApplicationFactory<Program> webApplication;
    private readonly HttpClient client;


    public UseCaseEvaluationIntegrationTests(WebApplicationFactory<Program> factory)
    {
        this.webApplication = factory;
        client = webApplication.CreateClient();
    }

    //Use Case 6
    [Trait("Integration", "PDPRequired")]
    [Theory]
    [InlineData("api/Patient/GetAllPatients")]
    public async Task UC1_When_Username_And_Password_Then_Token(string url)
    {
        string username = "Peter";
        string password = "Schmidt";

        var user = EmployeeCollection.GetEmployee(username);
        Assert.NotNull(user);
        var bearerToken = await GetValidBearerToken(username, password);
        Assert.NotNull(bearerToken);
    }


    [Trait("Integration", "PDPRequired")]
    [Theory]
    [InlineData("api/Patient/GetAllPatients")]
    public async Task UC2a_When_User_Is_SeniorConsultant_Then_All_Patients_Available(string url)
    {
        string username = "Robert";
        string password = "Keinel";

        var user = EmployeeCollection.GetEmployee(username);
        Assert.NotNull(user);
        Assert.True(user.Role == EmployeeRole.SeniorConsultantDoctor);

        var bearerToken = await GetValidBearerToken(username, password);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        patientsResponse.EnsureSuccessStatusCode();
        var patients = await patientsResponse.Content.ReadAsStringAsync();
        Assert.False(string.IsNullOrEmpty(patients));
        List<Patient>? patientList = JsonConvert.DeserializeObject<List<Patient>>(patients);
        PatientController controller = new PatientController();
        Assert.True(controller.GetAllPatients().Count() == patientList!.Count);
    }

    [Trait("Integration", "PDPRequired")]
    [Theory]
    [InlineData("api/Patient/GetAllPatientsUserForJsonFilterBlackenDiagnosis")]
    public async Task UC2b_When_JsonFilterAndBlackenDiagnosisObligation_And_Constrainthandler_then_Diagnosis_is_XXX(string url)
    {
        string username = "Peter";
        string password = "password";
        var bearerToken = await GetValidBearerToken(username, password);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        patientsResponse.EnsureSuccessStatusCode();
        var content = await patientsResponse.Content.ReadAsStringAsync();
        var patients = JsonConvert.DeserializeObject<IEnumerable<Patient>>(content);
        Assert.NotNull(patients);
        Assert.True(patients.All(p => p.Diagnosis.Equals("XXX")));
    }



    [Trait("Integration", "PDPRequired")]
    [Theory]
    [InlineData("api/Patient/GetAllPatients")]
    public async Task When_User_Is_ClinicalAssistant_then_no_Access_Denied(string url)
    {
        string username = "Peter";
        string password = "Schmidt";

        var user = EmployeeCollection.GetEmployee(username);
        Assert.NotNull(user);
        Assert.True(user.Role == EmployeeRole.ClinicalAssistant);

        var bearerToken = await GetValidBearerToken(username, password);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        //patientsResponse.EnsureSuccessStatusCode();
        var patients = await patientsResponse.Content.ReadAsStringAsync();
        Assert.True(patients.Contains("Access denied"));
    }



    [Trait("Integration", "PDPRequired")]
    [Theory]
    [InlineData("api/Patient/GetPatientByIdForPatient/4")]
    public async Task UC3_When_Patient_With_Id_5_Then_Id_Replaced_To5(string url)
    {
        string rightPatientId = 5.ToString();
        string username = $"{nameof(User.Username)}{rightPatientId}";
        string password = $"{nameof(User.Password)}{rightPatientId}";

        var bearerToken = await GetValidBearerToken(username, password);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        patientsResponse.EnsureSuccessStatusCode();
        var content = await patientsResponse.Content.ReadAsStringAsync();
        var patient = JsonConvert.DeserializeObject<Patient>(content);
        Assert.NotNull(patient);
        Assert.True(patient.Id.Equals(rightPatientId));
    }


    [Trait("Integration", "PDPRequired")]
    [Theory]
    [InlineData("api/Patient/GetAllPatientsForTypedFilter")]
    public async Task UC4_When_Employee_Department_Cancer_Then_Patients_Department_Cancer(string url)
    {

        //Credentials for Employee of Department Cancer
        string username = "Peter";
        string password = "Schmidt";

        var user = EmployeeCollection.GetEmployee(username);
        Assert.NotNull(user);

        var bearerToken = await GetValidBearerToken(username, password);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        patientsResponse.EnsureSuccessStatusCode();
        var content = await patientsResponse.Content.ReadAsStringAsync();
        var patients = JsonConvert.DeserializeObject<IEnumerable<Patient>>(content);
        Assert.NotNull(patients);
        Assert.True(patients.All(p => p.Department.Equals(user!.Department)));
    }


    //Use Case 5
    [Trait("Integration", "PDPRequired")]
    [Theory]
    [InlineData("api/Patient/GetAllPatientsForJsonFilterDelete")]
    public async Task UC5_When_Diagnosis_Broken_Leg_Then_Patients_Deleted(string url)
    {
        string rightPatientId = 5.ToString();
        string username = $"{nameof(User.Username)}{rightPatientId}";
        string password = $"{nameof(User.Password)}{rightPatientId}";

        var bearerToken = await GetValidBearerToken(username, password);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        patientsResponse.EnsureSuccessStatusCode();
        var content = await patientsResponse.Content.ReadAsStringAsync();
        var patients = JsonConvert.DeserializeObject<IEnumerable<Patient>>(content);
        Assert.NotNull(patients);
        Assert.True(patients.All(p => string.IsNullOrEmpty(p.Diagnosis) || !p.Diagnosis.Equals("Broken Leg")));
    }


    //Use Case 6
    [Trait("Integration", "PDPRequired")]
    [Theory]
    [InlineData("api/Patient/GetAllPatientsUserForJsonFilterReplaceAddress")]
    public async Task UC6_When_Policy_Replace_Street_And_City_then_both_replaced_with_anonym(string url)
    {
        string replacement = "anonym";
        string username = "Peter";
        string password = "Schmidt";

        var user = EmployeeCollection.GetEmployee(username);
        Assert.NotNull(user);
        Assert.True(user.Role == EmployeeRole.ClinicalAssistant);

        var bearerToken = await GetValidBearerToken(username, password);
        var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", bearerToken);
        HttpResponseMessage patientsResponse = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
        patientsResponse.EnsureSuccessStatusCode();
        var content = await patientsResponse.Content.ReadAsStringAsync();
        var patients = JsonConvert.DeserializeObject<IEnumerable<Patient>>(content);
        Assert.NotNull(patients);
        Assert.True(patients.All(p => p.Address.Equals(replacement) && p.City.Equals(replacement)));
    }







    private async Task<string> GetValidBearerToken(string username, string password)
    {
        var userName = username; //"Robert";
        User? user = EmployeeCollection.GetEmployee(userName);
        if (user == null)
        {
            user = PatientCollection.GetPatient(userName);
        }
        var userJObject = new JObject
        {
            { nameof(User.Username), user!.Username },
            { nameof(User.Password), password }
            //{ nameof(User.ApiKey), "ApiKey" }
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
#endif