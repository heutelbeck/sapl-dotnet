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

using System.ComponentModel.DataAnnotations;
using SAPL.WebAPIDemo.ExampleData.Data;

namespace SAPL.WebAPIDemo.ExampleData.Models
{
    public class Patient : User
    {
        private string? diagnosis;

        public Patient()
        {

        }

        public Patient(string id)
        {
            Random rnd = new Random();
            Id = id;
            FirstName = $"{nameof(State)}{Id}";
            LastName = $"{nameof(State)}{Id}";
            Username = $"{nameof(Username)}{Id}";
            Password = $"{nameof(Password)}{Id}";
            Email = $"{nameof(Email)}{Id}@server.com";
            Address = $"Street {Id}";
            City = $"{nameof(City)}{Id}";
            State = $"{nameof(State)}{Id}";
            Zip = rnd.Next(5).ToString();
            Country = $"{nameof(Country)}{Id}";
            Phone = rnd.Next(10).ToString();
            DiagnosisId = rnd.Next(1, 15).ToString();
            Diagnosis = DiagnosisCollection.GetDiagnosis(DiagnosisId)!.Description;
            Department = Department.Pediatry;
        }

        //public string Id { get; set; } = null!;
        //public string FirstName { get; set; } = null!;
        //public string LastName { get; set; } = null!;

        [DataType(DataType.EmailAddress)]
        //public string Email { get; set; } = null!;

        public string Address { get; set; } = null!;
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string Zip { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string Phone { get; set; } = null!;
        public Department Department { get; set; }
        public string DepartmentDisplayName => this.Department.ToString();
        public string DiagnosisId { get; set; } = null!;

        public string Diagnosis
        {
            get
            {
                //if (diagnosis == null)
                //{
                //    return DiagnosisCollection.GetDiagnosis(DiagnosisId)!.Description;
                //}

                return diagnosis!;
            }
            set
            {
                diagnosis = value;
            }
        }

    }
}
