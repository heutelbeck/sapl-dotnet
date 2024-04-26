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

namespace SAPL.WebAPIDemo.ExampleData.Models
{
    public class Employee : User
    {
        public string Address { get; set; } = null!;
        public string City { get; set; } = null!;
        public string State { get; set; } = null!;
        public string Zip { get; set; } = null!;
        public string Country { get; set; } = null!;
        public string Phone { get; set; } = null!;

        public EmployeeStatus Status { get; set; }
        //Nurse, Doctor, 
        public EmployeeRole Role { get; set; }
        public Department Department { get; set; }

    }
}