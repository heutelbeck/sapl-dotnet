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
    /// Provides authorization functionalities, implements IObservable to notify about authorization decisions, 
    /// and IAuthorizationProvider for authorization services.
    /// </summary>
    public class AuthorizationProvider : IObservable<AuthorizationDecision>, IAuthorizationProvider
    {
        // A list to keep track of all observers interested in authorization decisions
        private List<IObserver<AuthorizationDecision>> observers = null!;
        // Manages connections with the Policy Decision Point (PDP)
        private IManagePDPConnection connectionManager = null!;
        // Used for thread-safe singleton instance creation
        private static readonly object padlock = new object();
        // Singleton instance of AuthorizationProvider
        private static AuthorizationProvider current = null!;
        // Indicates if the provider has been initialized
        private bool initialized;

        // Private constructor to prevent instance creation
        private AuthorizationProvider() { }

        // Public property to access the list of observers
        public List<IObserver<AuthorizationDecision>> Observers => observers;

        // Singleton access to the current instance of AuthorizationProvider
        public static AuthorizationProvider Current
        {
            get
            {
                lock (padlock)
                {
                    if (current == null)
                    {
                        current = new AuthorizationProvider();
                    }
                    return current;
                }
            }
        }

        // Initializes the connection manager, ensuring it's only done once
        void IAuthorizationProvider.InitializeConnection(IManagePDPConnection connectionManager)
        {
            if (!initialized)
            {
                this.connectionManager = connectionManager;
                observers = new List<IObserver<AuthorizationDecision>>();
                initialized = true;
            }
        }

        // Subscribes an observer to the list, ensuring it's only added once
        public IDisposable Subscribe(IObserver<AuthorizationDecision> observer)
        {
            if (!observers.Contains(observer))
            {
                observers.Add(observer);
            }
            return new Unsubscriber(observers, observer);
        }

        // Tracks a decision request and notifies the observer
        void IAuthorizationProvider.TrackDecision(AuthorizationSubscription subscription, IObserver<AuthorizationDecision> observer, string relativeUri)
        {
            if (subscription == null)
            {
                observer.OnError(new AuthorizationDecisionUnknownException());
            }
            else
            {
                connectionManager.SubscribeDecision(subscription, observer, relativeUri);
            }
        }

        // Similar to TrackDecision but for one-time decision tracking
        void IAuthorizationProvider.TrackDecisionOnce(AuthorizationSubscription subscription, IObserver<AuthorizationDecision> observer, string relativeUri)
        {
            if (subscription == null)
            {
                observer.OnError(new AuthorizationDecisionUnknownException());
            }
            else
            {
                connectionManager.SubscribeDecisionOnce(subscription, observer, relativeUri);
            }
        }

        // Retrieves an AuthorizationPublisher by subscriptionId, if available
        public AuthorizationPublisher? GetPublisher(string subscriptionId)
        {
            if (observers != null && observers.Any())
            {
                return observers.Cast<AuthorizationPublisher>()
                    .FirstOrDefault(o => o.SubscriptionId.Equals(subscriptionId));
            }
            return null;
        }

        // Retrieves an AuthorizationPublisher by AuthorizationSubscription, if available
        public AuthorizationPublisher GetPublisher(AuthorizationSubscription subscription)
        {
            if (observers != null && observers.Any())
            {
                return observers.Cast<AuthorizationPublisher>()
                    .FirstOrDefault(o => o.Subscription.Equals(subscription));
            }
            return null!;
        }

        // Notifies all observers that transmission is complete and clears the list of observers
        public void EndTransmission()
        {
            foreach (var observer in observers.ToArray())
                if (observers.Contains(observer))
                    observer.OnCompleted();

            observers.Clear();
        }
    }

    /// <summary>
    /// Handles unsubscription from the observers list upon disposal.
    /// </summary>
    internal class Unsubscriber : IDisposable
    {
        private List<IObserver<AuthorizationDecision>> observers;
        private IObserver<AuthorizationDecision> _observer;

        public Unsubscriber(List<IObserver<AuthorizationDecision>> observers, IObserver<AuthorizationDecision> observer)
        {
            this.observers = observers;
            this._observer = observer;
        }

        // Removes the observer from the list upon disposal
        public void Dispose()
        {
            if (_observer != null && observers.Contains(_observer))
            {
                observers.Remove(_observer);
            }
        }
    }
}