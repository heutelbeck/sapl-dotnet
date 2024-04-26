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
    public class DiagnosisCollection
    {
        private static List<Diagnosis?>? diagnoses;

        public static List<Diagnosis?> Diagnoses
        {
            get
            {
                if (diagnoses == null)
                {
                    diagnoses = new List<Diagnosis?>()
                    {
                        new()
                        {
                            Id = "0",
                            Issue_Date = DateTime.Now,
                            Description = "Unknown",
                            Treatment = "Unknown",
                            Frequency = "Unknown"
                        },
                        new()
                        {
                            Id = "1",
                            Issue_Date = DateTime.Now,
                            Description = "Broken Leg",
                            Treatment = "Physiotherapy",
                            Frequency = "Once a month"
                        },
                        new()
                        {
                            Id = "2",
                            Issue_Date = DateTime.Now,
                            Description = "Mood disorders",
                            Treatment = "Aromatherapy",
                            Frequency = "Twice a week"
                        },
                        new()
                        {
                            Id = "3",
                            Issue_Date = DateTime.Now,
                            Description = "Inflammations",
                            Treatment = "Cyrotherapy",
                            Frequency = "Twice a month"
                        },
                        new()
                        {
                            Id = "4",
                            Issue_Date = DateTime.Now,
                            Description = "Newborn Jaundice",
                            Treatment = "Phototherapy",
                            Frequency = "Once a month"
                        },
                        new()
                        {
                            Id = "5",
                            Issue_Date = DateTime.Now,
                            Description = "Cancer",
                            Treatment = "Radiotherapy",
                            Frequency = "Once in 3 months"
                        },
                        new()
                        {
                            Id = "6",
                            Issue_Date = DateTime.Now,
                            Description = "Cancer",
                            Treatment = "Immunotherapy",
                            Frequency = "Once a month"
                        },
                        new()
                        {
                            Id = "7",
                            Issue_Date = DateTime.Now,
                            Description = "Unknown",
                            Treatment = "Monotherapy",
                            Frequency = "Once a month"
                        },
                        new()
                        {
                            Id = "8",
                            Issue_Date = DateTime.Now,
                            Description = "Unknown",
                            Treatment = "Pharmacotherapy",
                            Frequency = "Once a month"
                        },
                        new()
                        {
                            Id = "9",
                            Issue_Date = DateTime.Now,
                            Description = "Hypoxemia",
                            Treatment = "Oxygen therapy",
                            Frequency = "Once a week"
                        },
                        new()
                        {
                            Id = "10",
                            Issue_Date = DateTime.Now,
                            Description = "Genetic defect",
                            Treatment = "Gene therapy",
                            Frequency = "Once a month"
                        },

                    };

                }

                for (int i = 0; i < diagnoses.Count; i++)
                {
                    diagnoses[i]!.Issue_Date = DateTime.Now.AddDays(i);
                }
                return diagnoses;
            }
        }
        public static Diagnosis? GetDiagnosis(string id)
        {
            if (Diagnoses.Any(d => d!.Id.Equals(id)))
            {
                return Diagnoses.FirstOrDefault(d => d!.Id.Equals(id));
            }
            return Diagnoses.FirstOrDefault(d => d!.Id.Equals("0"));
        }

        public void Add(Diagnosis diagnosis)
        {
            diagnosis.Id = (Diagnoses.Count() + 1).ToString();
            Diagnoses.Add(diagnosis);
        }
    }
}
