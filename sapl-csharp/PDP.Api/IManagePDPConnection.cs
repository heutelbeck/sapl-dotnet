// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// 
//      http://www.apache.org/licenses/LICENSE-2.0
// 
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace SAPL.PDP.Api
{
    /// <summary>
    /// Defines the contract for managing connections with a Policy Decision Point (PDP).
    /// </summary>
    public interface IManagePDPConnection
    {
        /// <summary>
        /// Subscribes to authorization decisions from a PDP based on a given subscription.
        /// This method is intended for continuous observation of authorization decisions.
        /// </summary>
        /// <param name="authzSubscription">The authorization subscription details.</param>
        /// <param name="observer">The observer that will receive authorization decisions.</param>
        /// <param name="relativeUri">The relative URI to identify the specific resource or action to monitor.</param>
        /// <returns>A task representing the asynchronous operation of the subscription.</returns>
        public Task SubscribeDecision(AuthorizationSubscription authzSubscription,
            IObserver<AuthorizationDecision> observer, string relativeUri);

        /// <summary>
        /// Subscribes to a single authorization decision from a PDP based on a given subscription.
        /// After the first decision is received, the subscription ends.
        /// </summary>
        /// <param name="subscription">The authorization subscription details.</param>
        /// <param name="observer">The observer that will receive the one-time authorization decision.</param>
        /// <param name="relativeUri">The relative URI for identifying the specific resource or action.</param>
        /// <returns>A task representing the asynchronous operation of the one-time subscription.</returns>
        public Task SubscribeDecisionOnce(AuthorizationSubscription subscription, IObserver<AuthorizationDecision> observer,
            string relativeUri);
    }
}
