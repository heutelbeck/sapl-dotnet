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
using SAPL.AspNetCore.Security.Filter.Web_API;
using SAPL.WebAPIDemo.ExampleData.Data;
using SAPL.WebAPIDemo.ExampleData.Models;

// For more information on enabling Web API for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace Web_API_Demo.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PatientController : ControllerBase
    {


        //Use Case 2a
        [HttpGet("[action]")]
        [PreEnforce]
        public IEnumerable<Patient> GetAllPatients()
        {
            return PatientCollection.All();
        }

        //Endpoint for Use Case 2b
        [HttpGet("[action]")]
        [PostEnforce]
        public IEnumerable<Patient> GetAllPatientsUserForJsonFilterBlackenDiagnosis()
        {
            return PatientCollection.All();
        }

        //Use Case 3
        [HttpGet("[action]/{id}")]
        [PreEnforce]
        //[PreEnforce(action:nameof(GetPatientById))]
        public IActionResult GetPatientByIdForPatient(int id)
        {
            var patient = PatientCollection.GetPatientById(id);
            return new OkObjectResult(patient);
        }

        //Endpoint for Use Case 4
        [HttpGet("[action]")]
        [PostEnforce]
        public IEnumerable<Patient> GetAllPatientsForTypedFilter()
        {
            return PatientCollection.All();
        }

        //Endpoint for Use Case 5
        [HttpGet("[action]")]
        [PostEnforce]
        public IEnumerable<Patient> GetAllPatientsForJsonFilterDelete()
        {
            return PatientCollection.All();
        }

        //Endpoint for Use Case 6
        [HttpGet("[action]")]
        [PostEnforce]
        public IEnumerable<Patient> GetAllPatientsUserForJsonFilterReplaceAddress()
        {
            return PatientCollection.All();
        }





        [HttpGet("[action]")]
        [PreEnforce]
        public IEnumerable<Patient> GetPatients()
        {
            return PatientCollection.All();
        }

        [HttpGet("[action]")]
        [PreEnforce(subject: "User", action: nameof(GetAllPatientsUser))]
        public IEnumerable<Patient> GetAllPatientsUser()
        {
            return PatientCollection.All();
        }

        [HttpGet("[action]")]
        [PostEnforce(subject: "User", action: nameof(GetAllPatientsUserForTypedFilter))]
        public IEnumerable<Patient> GetAllPatientsUserForTypedFilter()
        {
            return PatientCollection.All();
        }



        [HttpGet("[action]")]
        [PostEnforce(subject: "User", action: nameof(GetAllPatientsUserForJsonFilterBlacken))]
        public IEnumerable<Patient> GetAllPatientsUserForJsonFilterBlacken()
        {
            return PatientCollection.All();
        }

        //GetAllPatientsUserForJsonFilterDelete

        [HttpGet("[action]")]
        [PostEnforce(subject: "User", action: nameof(GetAllPatientsUserForJsonFilterDelete))]
        public IEnumerable<Patient> GetAllPatientsUserForJsonFilterDelete()
        {
            return PatientCollection.All();
        }

        [HttpGet("[action]")]
        [PostEnforce(subject: "User", action: nameof(GetAllPatientsUserForJsonFilter))]
        public IEnumerable<Patient> GetAllPatientsUserForJsonFilter()
        {
            return PatientCollection.All();
        }

        [HttpGet("[action]")]
        [PostEnforce]
        public IEnumerable<Patient> GetAllPatientsForJsonFilterReplace()
        {
            return PatientCollection.All();
        }

        [HttpGet("[action]")]
        [PreEnforce]
        public IEnumerable<Patient> GetPatientsWithBrokenLeg()
        {
            return PatientCollection.PatientsWithBrokenLegs();
        }


        [HttpGet("[action]")]
        [PreEnforce]
        public IEnumerable<Patient> GetPatientsWithCancer()
        {
            return PatientCollection.PatientsWithCancer();
        }


        // GET api/<PatientController>/5
        [HttpGet("[action]/{id}")]
        [PreEnforce]
        //[PreEnforce(action:nameof(GetPatientById))]
        public IActionResult GetPatientById(int id)
        {
            var patient = PatientCollection.GetPatientById(id);
            return new OkObjectResult(patient);
        }






        // POST api/<PatientController>
        [HttpPost("[action]")]
        public void AddPatient([FromBody] Patient patient)
        {
            PatientCollection.Insert(patient);
        }

        // PUT api/<PatientController>/5
        [HttpPut("[action]/{id}")]
        public void UpdatePatient(int id, [FromBody] Patient patient)
        {
            PatientCollection.ReplaceOrInsert(id, patient);
        }

        // DELETE api/<PatientController>/5
        [HttpDelete("[action]/{id}")]
        public void DeletePatient(int id)
        {
            PatientCollection.Delete(id);
        }
    }
}
