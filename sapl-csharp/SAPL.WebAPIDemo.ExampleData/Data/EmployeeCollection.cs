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

using SAPL.WebAPIDemo.ExampleData.Models;

namespace SAPL.WebAPIDemo.ExampleData.Data
{
    public static class EmployeeCollection
    {
        private static List<Employee?> employees = null!;

        public static List<Employee?> Employees
        {
            get
            {
                if (employees == null)
                {
                    employees = new List<Employee?>()
                    {
                        new()
                    {
                        Id = "3",
                        Address = "Müllerstraße 16",
                        City = "Wiesbaden",
                        Country = "Deutschland",
                        Department = Department.Cancer,
                        Email = "robert.keinel@test-online.de",
                        FirstName = "Robert", LastName = "Keinel",
                        Password = "password", Username = "Robert",
                        Phone = "0613112345678",
                        Role = EmployeeRole.SeniorConsultantDoctor
                    },
                        new()
                        {
                            Id = "12",
                            Address = "Müllerstraße 16",
                            City = "Wiesbaden",
                            Country = "Deutschland",
                            Department = Department.Cancer,
                            Email = "robert.keinel@test-online.de",
                            FirstName = "Peter", LastName = "Schmidt",
                            Password = "password", Username = "Peter",
                            Phone = "0613112345678",
                            Role = EmployeeRole.ClinicalAssistant
                        }

                    };

                }
                return employees;
            }
        }
        public static Employee? GetEmployee(string userName)
        {
            var result = Employees.FirstOrDefault(e => e!.Username.Equals(userName));
            ClearPassword(result);
            return result;
        }

        public static bool IsEmployee(string username)
        {
            return GetEmployee(username) != null;

        }

        private static void ClearPassword(User? result)
        {
            if (result != null)
            {
                result.Password = string.Empty;
            }
        }
    }
}
