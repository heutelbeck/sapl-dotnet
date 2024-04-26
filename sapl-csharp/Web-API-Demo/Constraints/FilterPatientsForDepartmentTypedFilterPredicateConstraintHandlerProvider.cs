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

using Newtonsoft.Json.Linq;
using SAPL.AspNetCore.Security.Constraints.Providers;
using SAPL.AspNetCore.Security.Subscription;
using SAPL.WebAPIDemo.ExampleData.Models;

namespace Web_API_Demo.Constraints
{
    // This class provides a constraint handler for filtering patients based on their department.
    // It extends TypedFilterPredicateConstraintHandlerProviderBase to apply filter predicates on Patient objects.
    public class FilterPatientsForDepartmentTypedFilterPredicateConstraintHandlerProvider : TypedFilterPredicateConstraintHandlerProviderBase<Patient>
    {
        // Field to store the name of the department to filter patients by.
        public string departmentToFilter = null!;

        /// <summary>
        /// Determines whether this handler is responsible for the given constraint.
        /// </summary>
        /// <param name="constraint">The JToken representing the constraint.</param>
        /// <returns>True if this handler should process the constraint; otherwise, false.</returns>
        public override bool IsResponsible(JToken constraint)
        {
            // Define the constraint type this handler is responsible for.
            string constraintType = "filterPatiensForDepartment";

            // Extract the obligation type from the constraint.
            string obligationType = ObligationContentReaderUtil.GetObligationType(constraint)!;

            // Check if the obligation type is not empty and matches the expected constraint type.
            if (!string.IsNullOrEmpty(obligationType))
            {
                // Retrieve the specific department to filter from the constraint.
                string filterValue = ObligationContentReaderUtil.GetPropertyValue(constraint, "filterDepartment")!;
                departmentToFilter = filterValue;

                // Return true if the obligation type matches the constraint type.
                return obligationType.Equals(constraintType);
            }

            // Return false if the constraint does not match the expected type.
            return false;
        }

        /// <summary>
        /// Provides a handler (predicate) for filtering patients by the specified department.
        /// </summary>
        /// <returns>A predicate that filters patients by their department.</returns>
        protected override Predicate<Patient> GetHandler()
        {
            // Return a predicate that checks if a patient belongs to the specified department.
            return p => p.Department.ToString().Equals(departmentToFilter);
        }
    }
}
