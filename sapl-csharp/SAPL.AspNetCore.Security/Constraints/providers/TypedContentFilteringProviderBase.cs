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

namespace SAPL.AspNetCore.Security.Constraints.Providers
{
    /// <summary>
    /// Abstract base class for typed content filtering providers.
    /// </summary>
    /// <typeparam name="T">The type of content to filter.</typeparam>
    public abstract class TypedContentFilteringProviderBase<T> : ITypedContentFilteringProvider
    {
        /// <summary>
        /// The constraint for the filtering provider.
        /// </summary>
        protected JToken constraint = null!;

        /// <summary>
        /// Gets the list of empty actions.
        /// </summary>
        /// <typeparam name="TT">The type of content for which actions are defined.</typeparam>
        /// <returns>The list of empty actions.</returns>
        public List<ValueTuple<Action<TT>, string>> GetEmptyActions<TT>()
        {
            return new List<(Action<TT>, string)>();
        }

        /// <summary>
        /// Gets the signal of the content filtering provider.
        /// </summary>
        /// <returns>The signal of the content filtering provider.</returns>
        public virtual ISignal.Signal GetSignal()
        {
            return ISignal.Signal.ON_EXECUTION;
        }

        bool IResponsibleConstraintHandlerProvider.IsResponsible(JToken constraint)
        {
            return IsResponsible(constraint);
        }

        object ITypedContentFilteringProvider.GetPredicatesAndActions()
        {
            return GetHandlerWithTransformation(constraint);
        }

        /// <summary>
        /// Checks if the provider is responsible for the given constraint.
        /// </summary>
        /// <param name="constraint">The constraint to check.</param>
        /// <returns><c>true</c> if the provider is responsible; otherwise, <c>false</c>.</returns>
        protected abstract bool IsResponsible(JToken constraint);

        /// <summary>
        /// Gets the handler with transformation for the content filtering provider.
        /// </summary>
        /// <param name="constraint">The constraint for the content filtering.</param>
        /// <returns>The handler with transformation.</returns>
        protected abstract List<(Predicate<T> predicate, List<(Action<T> action, string actionType)> actions)> GetHandlerWithTransformation(JToken constraint);
    }
}
