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

using SAPL.AspNetCore.Security.Constraints.Providers;
using SAPL.WebAPIDemo.ExampleData.Models;

namespace Web_API_Demo.Constraints
{
    // This class provides a constraint handler specifically for filtering patients 
    // based on a predefined criterion. It extends from the TypedFilterPredicateConstraintHandlerProviderBase,
    // applying the filter predicates specifically to Patient objects.
    public class FilterPatientsForUserTypedFilterPredicateConstraintHandlerProvider : TypedFilterPredicateConstraintHandlerProviderBase<Patient>
    {
        /// <summary>
        /// Provides a handler (predicate) for filtering patients.
        /// In this implementation, it filters patients by last name, specifically matching "Blubb".
        /// </summary>
        /// <returns>A predicate function that filters patients based on the last name.</returns>
        protected override Predicate<Patient> GetHandler()
        {
            // Returns a predicate that filters patients by their last name, checking for a match with "Blubb".
            return p => p.LastName.Equals("Blubb");
        }
    }
}