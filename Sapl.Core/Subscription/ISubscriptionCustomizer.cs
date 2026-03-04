namespace Sapl.Core.Subscription;

public interface ISubscriptionCustomizer
{
    void Customize(SubscriptionContext context, SubscriptionBuilder builder);
}
