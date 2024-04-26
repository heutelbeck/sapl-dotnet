﻿/*
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

namespace SAPL.AspNetCore.Security.Constraints.api
{
    /// <summary>
    /// Defines an interface for a provider that offers typed predicate constraint handling mechanisms.
    /// This interface is part of a security constraints framework, focusing on evaluating predicates
    /// tailored to specific data types.
    /// </summary>
    public interface ITypedPredicateConstraintHandlerProvider : IResponsibleConstraintHandlerProvider
    {
        /// <summary>
        /// Retrieves a predicate object for evaluating constraints specific to a particular data type.
        /// This method is responsible for providing the logic to evaluate constraints based on the type
        /// of data being processed, allowing for customized constraint handling mechanisms.
        /// </summary>
        /// <returns>A predicate object used to evaluate constraints for a specific data type.</returns>
        object GetPredicate();
    }
}