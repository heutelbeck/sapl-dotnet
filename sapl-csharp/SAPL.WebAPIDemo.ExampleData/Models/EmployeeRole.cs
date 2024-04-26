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
    public enum EmployeeRole
    {
        //Doctors
        SeniorConsultantDoctor, //specialist doctor who sees patients at specific times
        RegistrarDoctor, //senior doctor who supervises residents, interns and students
        ResidentDoctor, //looks after patients on the ward and is in training for specialisation
        InternDoctor, //has completed her/his studies and is now finishing her/his final year in hospital
        StudentDoctor, //undergraduate medical student

        //Nurse
        UnitManagerNurse, //runs the ward
        PractitionerNurse, //highly skilled nurses with an advanced level of training
        SpecialistNurse, //such as clinical nurse specialists, clinical nurse consultants, clinical nurse educators, triage nurses, emergency department nurses
        RegisteredNurses, //provides a high level of day-to-day care and perform some minor procedures
        EnrolledNurse, //provide basic medical care under the supervision of more senior nurses.

        //Allied health professionals
        Dietitians,
        OccupationalTherapists,
        Pharmacists,
        Physiotherapists,
        Podiatrist,
        SpeechPathologists,

        //Other hospital staff
        ClinicalAssistant, //take care of ward housekeeping
        PatientServicesAssistant, // brings meals and drinks
        Porter, //takes care of patient lifting and transport
        Volunteers,//helps with fundraising and ward visits
        WardClerks //staff the ward reception desks (Stationsschreiber)


    }
}
