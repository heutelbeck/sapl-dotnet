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
using SAPL.AspNetCore.Security.Constraints.api;
using SAPL.AspNetCore.Security.Subscription;

namespace SAPL.AspNetCore.Security.Constraints.Providers
{
    /// <summary>
    /// Base class for constraint handlers to filter the result of the endpoint.
    /// </summary>
    /// <typeparam name="T">The type of content to filter.</typeparam>
    public abstract class TypedFilterPredicateConstraintHandlerProviderBase<T> : ITypedPredicateConstraintHandlerProvider
    {
        /// <summary>
        /// Gets the signal of the constraint handler.
        /// </summary>
        /// <returns>The signal of the constraint handler.</returns>
        public virtual ISignal.Signal GetSignal()
        {
            return ISignal.Signal.ON_EXECUTION;
        }

        /// <summary>
        /// Checks if the handler is responsible for the given constraint.
        /// </summary>
        /// <param name="constraint">The constraint to check.</param>
        /// <returns><c>true</c> if the handler is responsible; otherwise, <c>false</c>.</returns>
        public virtual bool IsResponsible(JToken constraint)
        {
            string constraintType = $"filter{typeof(T).Name}s";
            string obligationType = ObligationContentReaderUtil.GetObligationType(constraint)!;
            if (!string.IsNullOrEmpty(obligationType))
            {
                return obligationType.Equals(constraintType);
            }
            return false;
        }

        object ITypedPredicateConstraintHandlerProvider.GetPredicate()
        {
            return GetHandler();
        }

        /// <summary>
        /// Gets the handler for the constraint.
        /// </summary>
        /// <returns>The handler for the constraint.</returns>
        protected abstract Predicate<T> GetHandler();
    }
}
