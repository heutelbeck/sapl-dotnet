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

using System.ComponentModel;
using csharp.sapl.pdp.remote;
using Microsoft.Extensions.Configuration;
using SAPL.PDP.Api;
using SAPL.PDP.Api.Configuration;
using SAPL.PDP.Client.SubscriptionCaching;

namespace SAPL.PDP.Client
{

    /// <summary>
    /// Encapsulates all available SubscriptionTypes to get a Decision
    /// </summary>
    public class PolicyDecisionPoint : IPolicyDecisionPoint
    {
        private static string DECIDEONCE = "/api/pdp/decide-once";
        private static string DECIDE = "/api/pdp/decide";
        public event PropertyChangedEventHandler? SubscriptionCacheUpdated;
        public PolicyDecisionPoint(IConfiguration? applicationSettings)
        {
            PdpConfiguration configuration = new PdpConfiguration(applicationSettings);
            if (configuration.IsValid())
            {
                SaplClient.Current.SetDeviatingCredentials(configuration.BaseUri, configuration.Username, configuration.Password);
            }
        }

        public PolicyDecisionPoint(string baseUri, string userName, string password)
        {
            SaplClient.Current.SetDeviatingCredentials(baseUri, userName, password);
        }

        /// <summary>
        /// Awaitable Task for getting a Decision for one time.
        /// </summary>
        /// <param name="authzSubscription"></param>
        /// <returns></returns>
        public Task<AuthorizationDecision> DecideOnce(AuthorizationSubscription authzSubscription)
        {
            return SaplClient.Current.DecideOnceAsync(authzSubscription, DECIDEONCE);
        }


        /// <summary>
        /// Awaitable Task for getting a cached Decision.
        /// All subscriptions are cached an updated on server sent events from SAPL-server
        /// </summary>
        /// <param name="authzSubscription"></param>
        /// <returns></returns>
        public Task<AuthorizationDecision> Decide(AuthorizationSubscription authzSubscription)
        {
            if (!SubscriptionCache.Current.ContainsSubscription(authzSubscription, out AuthorizationDecision decision))
            {
                var subscription = SubscribeToDecision(authzSubscription);
                ((AuthorizationPublisher)subscription).PropertyChanged += OnSubscribedDecisionChanged;
                return DecideOnce(authzSubscription);
            }

            return Task.FromResult(decision);
        }

        //Decide
        /// <summary>
        /// Observe a Decision for a single subscription
        /// </summary>
        /// <param name="authzSubscription"></param>
        /// <returns></returns>
        public IObserver<AuthorizationDecision> SubscribeToDecision(AuthorizationSubscription authzSubscription)
        {
            if (AuthorizationProvider.Current.GetPublisher(authzSubscription) != null)
            {
                return AuthorizationProvider.Current.GetPublisher(authzSubscription);
            }

               ((IAuthorizationProvider)AuthorizationProvider.Current).InitializeConnection(SaplClient.Current);
            var publisher = new AuthorizationPublisher(authzSubscription);
            publisher.Subscribe(AuthorizationProvider.Current);
            ((IAuthorizationProvider)AuthorizationProvider.Current).TrackDecision(authzSubscription, publisher, DECIDE);
            return publisher;
        }

        private void OnSubscribedDecisionChanged(object sender, PropertyChangedEventArgs e)
        {
            if (sender is AuthorizationPublisher publisher && e.PropertyName.Equals(nameof(AuthorizationPublisher.AuthorizationDecision)))
            {
                SubscriptionCache.Current.CacheDecision(publisher.Subscription, publisher.AuthorizationDecision);
                OnSubscriptionCacheUpdated();
            }
        }
        protected virtual void OnSubscriptionCacheUpdated()
        {
            SubscriptionCacheUpdated?.Invoke(this, new PropertyChangedEventArgs("SubscriptionCacheUpdated"));
        }

    }
}
