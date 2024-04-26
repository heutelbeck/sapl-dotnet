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

using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using SAPL.AspNetCore.Security.Filter.Web_API;
using SAPL.WebAPIDemo.ExampleData.Data;
using SAPL.WebAPIDemo.ExampleData.Models;

namespace SignalR_Demo.Hubs
{
    public class ChatHub : Hub
    {
        [PreEnforce("SomeSubject", "SomeAction", "SomeRecource", "SomeEnvironment")]
        public async Task SendMessage(string user, string message)
        {
            var patient = new Patient();

            if (!string.IsNullOrEmpty(user))
            {
                patient = PatientCollection.GetPatientById(Convert.ToInt32(user));
            }

            if (patient == null && !string.IsNullOrEmpty(message))
            {
                patient = PatientCollection.Patients.Where(p => p != null && p.LastName.Equals(message)).FirstOrDefault();
            }

            if (patient == null)
            {
                patient = new Patient();
            }
            string jsonString = JsonSerializer.Serialize(patient);

            await SendPatient(user, jsonString);
        }

        private async Task SendPatient(string user, string jsonString)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, jsonString);
        }
    }
}
