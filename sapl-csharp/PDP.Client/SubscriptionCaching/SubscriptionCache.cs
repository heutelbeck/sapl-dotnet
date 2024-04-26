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

using SAPL.PDP.Api;

namespace SAPL.PDP.Client.SubscriptionCaching
{
    public class SubscriptionCache
    {
        private static readonly object padlock = new object();
        private static SubscriptionCache current = null!;

        private SubscriptionCache() { }

        public static SubscriptionCache Current
        {
            get
            {
                lock (padlock)
                {
                    if (current == null)
                    {
                        current = new SubscriptionCache();
                        Initialize();
                    }
                    return current;
                }
            }
        }

        private static Dictionary<AuthorizationSubscription, AuthorizationDecision> cachedSingleDecicions = null!;

        public void CacheDecision(AuthorizationSubscription subscription, AuthorizationDecision decision)
        {
            if (subscription == null || decision == null)
            {
                return;
            }
            if (!ContainsSubscription(subscription, out AuthorizationDecision authorizationDecision))
            {
                cachedSingleDecicions.Add(subscription, decision);
            }
            else if (!cachedSingleDecicions[subscription].Equals(decision))
            {
                cachedSingleDecicions.Remove(subscription);
                cachedSingleDecicions.Add(subscription, decision);
            }
        }

        public bool ContainsSubscription(AuthorizationSubscription subscription, out AuthorizationDecision decision)
        {
            decision = cachedSingleDecicions.FirstOrDefault(k => k.Key != null && k.Value != null && k.Key.Equals(subscription)).Value;
            return decision != null;
        }

        private static void Initialize()
        {
            cachedSingleDecicions = new Dictionary<AuthorizationSubscription, AuthorizationDecision>();
        }
    }
}