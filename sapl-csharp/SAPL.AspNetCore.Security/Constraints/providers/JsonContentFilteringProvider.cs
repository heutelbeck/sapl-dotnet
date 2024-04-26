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
using SAPL.AspNetCore.Security.Constraints.Providers.Utils;
using SAPL.AspNetCore.Security.Subscription;

namespace SAPL.AspNetCore.Security.Constraints.Providers
{
    /// <summary>
    /// Provider for handling JSON content filtering based on constraints.
    /// </summary>
    /// <typeparam name="T">The type of the object.</typeparam>
    internal class JsonContentFilteringProvider<T> : IJsonContentFilteringProvider where T : class
    {
        private const string CONSTRAINT_TYPE = "filterJsonPathContent";
        protected JToken constraint = null!;

        /// <summary>
        /// Gets the signal for the constraint.
        /// </summary>
        /// <returns>The signal for the constraint.</returns>
        public ISignal.Signal GetSignal()
        {
            return ISignal.Signal.ON_EXECUTION;
        }

        /// <summary>
        /// Determines if the provider is responsible for the given constraint.
        /// </summary>
        /// <param name="constraint">The constraint to check.</param>
        /// <returns>True if the provider is responsible, otherwise false.</returns>
        public virtual bool IsResponsible(JToken constraint)
        {
            var obligationType = ObligationContentReaderUtil.GetObligationType(constraint);
            if (obligationType != null && obligationType.Equals(CONSTRAINT_TYPE))
            {
                this.constraint = constraint;
                return true;
            }
            return false;
        }

        /// <summary>
        /// Gets the handler for filtering JSON content.
        /// </summary>
        /// <returns>The handler for filtering JSON content.</returns>
        object IJsonContentFilteringProvider.GetHandler()
        {
            return GetHandler<T, T>(constraint);
        }

        /// <summary>
        /// Gets the handler for filtering JSON content.
        /// </summary>
        /// <typeparam name="TT">The type of the object to filter.</typeparam>
        /// <typeparam name="TU">The type of the filtered object.</typeparam>
        /// <param name="constraint">The constraint for filtering.</param>
        /// <returns>The handler for filtering JSON content.</returns>
        protected virtual Func<TT, TT?> GetHandler<TT, TU>(JToken constraint) where TT : class
        {
            return JsonContentFilterUtil.GetHandler<TT, TT>(constraint);
        }
    }
}
