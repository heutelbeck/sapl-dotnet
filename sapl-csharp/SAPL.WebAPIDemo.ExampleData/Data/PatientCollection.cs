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
    public static class PatientCollection
    {
        private static List<Patient?> patients = null!;

        public static List<Patient?> Patients
        {
            get
            {
                if (patients == null)
                {
                    patients = new List<Patient?>()
                    {
                        new Patient()
                        {
                            Id = "1",
                            Address = "Weserstraße 96",
                            City = "Mainz",
                            Country = "Deutschland",
                            Department = Department.Cancer,
                            Email = "anna.kneinel@kneinel-online.de",
                            FirstName = "Anna",
                            LastName = "Kneinel",
                            Password = "password",
                            Username = "AnnaKneinel",
                            Phone = "0613112345678",
                            DiagnosisId = "1"
                        },
                        new Patient()
                        {
                            Id = "2",
                            Address = "Weserstraße 66",
                            City = "Mainz",
                            Country = "Deutschland",
                            Department = Department.Cancer,
                            Email = "rebecca.blubb@blubb-online.de",
                            FirstName = "Rebecca",
                            LastName = "Blubb",
                            Password = "password",
                            Username = "Rebeccablubb",
                            Phone = "0613112345678",
                            DiagnosisId = "5"
                        },
                        new Patient(4.ToString()),
                        new Patient(5.ToString()),
                        new Patient(6.ToString()),
                        new Patient(7.ToString()),
                        new Patient(8.ToString()),
                        new Patient(9.ToString()),
                        new Patient(10.ToString())
                    };
                }
                return patients;
            }
        }


        public static Patient? GetPatient(string userName)
        {
            var result = Patients.FirstOrDefault(e => e!.Username.Equals(userName));
            ClearPassword(result);
            return result;
        }


        public static bool IsPatient(string username)
        {
            return GetPatient(username) != null;
        }

        public static IEnumerable<Patient> All()
        {
            var clearedPatients = Patients.ToArray();
            foreach (var patient in clearedPatients)
            {
                ClearPassword(patient);
            }
            return clearedPatients!;
        }

        public static IEnumerable<Patient> PatientsWithBrokenLegs()
        {
            return All().Where(p => p.DiagnosisId.Equals("1"));
        }

        public static IEnumerable<Patient> PatientsWithCancer()
        {
            return All().Where(p => p.DiagnosisId.Equals("5"));
        }

        public static Patient? GetPatientById(int id)
        {
            return All().FirstOrDefault(p => p.Id.Equals(id.ToString()));
        }

        private static void ClearPassword(Patient? result)
        {
            if (result != null)
            {
                result.Password = string.Empty;
            }
        }
        public static void Delete(int id)
        {
            Patients.Remove(Patients.Find(p => p!.Id.Equals(id.ToString())));
        }

        public static void Insert(Patient patient)
        {
            Patients.Add(patient);
        }

        public static void ReplaceOrInsert(int id, Patient patient)
        {
            if (GetPatientById(id) != null)
            {
                Delete(id);
                patient.Id = id.ToString();
            }
            else
            {
                patient.Id = (Patients.Max(p => id) + 1).ToString();
            }
            Insert(patient);
        }
    }
}
