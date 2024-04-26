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

using System;
using System.ComponentModel;
using System.Threading.Tasks;

namespace SAPL.PDP.Api
{
    /// <summary>
    /// The policy decision point is the component in the system, which will take an
    /// authorization subscription, retrieve matching policies from the policy retrieval point,
    /// evaluate the policies while potentially consulting external resources (e.g., through
    /// attribute finders), and return a {@link Flux} of authorization decision objects.
    ///
    /// This interface offers methods to hand over an authorization subscription to the policy
    /// decision point, differing in the construction of the underlying authorization
    /// subscription object.
    /// </summary>
    public interface IPolicyDecisionPoint
    {

        /// <summary>
        /// Takes an authorization subscription object and returns a decision
        /// from cached subscription or creates an new one added to the cache
        /// </summary>
        /// <param name="authzSubscription"></param>
        /// <returns></returns>
        Task<AuthorizationDecision> Decide(AuthorizationSubscription authzSubscription);

        /// <summary>
        /// Takes an authorization subscription object and returns a decision
        /// from cached subscription or creates an new one added to the cache
        /// </summary>
        /// <param name="authzSubscription"></param>
        /// <returns></returns>
        Task<AuthorizationDecision> DecideOnce(AuthorizationSubscription authzSubscription);

        /// <summary>
		///  Takes an authorization subscription object and returns a {@link Flux} emitting
		/// matching authorization decisions.
		/// @param authzSubscription the SAPL authorization subscription object
		/// @return a {@link Flux} emitting the authorization decisions for the given
		/// authorization subscription. New authorization decisions are only added to the
		/// stream if they are different from the preceding authorization decision.
		/// </summary>
		IObserver<AuthorizationDecision> SubscribeToDecision(AuthorizationSubscription authzSubscription);


        event PropertyChangedEventHandler SubscriptionCacheUpdated;

    }
}
