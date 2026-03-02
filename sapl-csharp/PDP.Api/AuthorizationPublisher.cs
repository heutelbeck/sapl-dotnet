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

using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace SAPL.PDP.Api
{
    /// <summary>
    /// Implements IObserver for AuthorizationDecision. It observes changes in authorization decisions.
    /// Implements INotifyDecisionChanged to notify changes in the decision.
    /// </summary>
    public class AuthorizationPublisher : IObserver<AuthorizationDecision>, INotifyDecisionChanged, IAuthorizationPublisher
    {
        // Holds the subscription to an observable provider
        private IDisposable unsubscriber = null!;
        // Represents the subscription details for authorization
        private AuthorizationSubscription subscription = null!;
        // Holds the last received authorization decision
        private AuthorizationDecision authorizationDecision;
        // Identifier for the subscription
        private string subscriptionId = null!;
        // Holds error information if any occurs
        private Exception error = null!;

        public Exception Error => error;
        public AuthorizationProvider Provider { get; private set; } = null!;

        public AuthorizationDecision AuthorizationDecision => authorizationDecision;
        public string SubscriptionId => subscriptionId;
        public AuthorizationSubscription Subscription => subscription;

        // Constructor with a subscription object
        public AuthorizationPublisher(AuthorizationSubscription subscription)
        {
            this.subscription = subscription;
            this.authorizationDecision = new AuthorizationDecision(Decision.INDETERMINATE);
        }

        // Constructor with a subscription ID
        public AuthorizationPublisher(string subscriptionId)
        {
            this.subscriptionId = subscriptionId;
            this.authorizationDecision = new AuthorizationDecision(Decision.INDETERMINATE);
        }

        // Subscribe to an observable authorization provider
#nullable enable
        public virtual void Subscribe(IObservable<AuthorizationDecision>? provider)
        {
            if (provider != null)
            {
                Provider = (AuthorizationProvider)provider;
                unsubscriber = provider.Subscribe(this);
            }
        }
#nullable disable
        // Method to call when subscription completes
        public virtual void OnCompleted()
        {
            Unsubscribe();
        }

        // Method to call when an error occurs in the subscription
        public virtual void OnError(Exception error)
        {
            this.authorizationDecision = new AuthorizationDecision(Decision.NOT_APPLICABLE);
            this.error = error;
            OnPropertyChanged(nameof(AuthorizationDecision));
        }

        // Method to call with the next authorization decision
        public virtual void OnNext(AuthorizationDecision value)
        {
            this.authorizationDecision = value;
            OnPropertyChanged(nameof(AuthorizationDecision));
        }

        // Unsubscribe from the observable
        public virtual void Unsubscribe()
        {
            unsubscriber.Dispose();
        }

        // Implementation of INotifyPropertyChanged to notify observers of property changes
        public event PropertyChangedEventHandler PropertyChanged;
#nullable enable
        // Notifies listeners about property changes
        protected virtual void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        // Helper method to set field value and notify property change
        protected bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            OnPropertyChanged(propertyName);
            return true;
        }
#nullable disable

        // Exposes the authorization decision as a business object
        public INotifyDecisionChanged AuthorizationDecisionBusinessObject => authorizationDecision;
    }
}
