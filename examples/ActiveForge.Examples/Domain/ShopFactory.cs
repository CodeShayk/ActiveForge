using ActiveForge;

namespace ActiveForge.Examples.Domain
{
    /// <summary>
    /// Application-level <see cref="FactoryBase"/> for the shop domain.
    /// Register polymorphic type substitutions here — e.g. if you have
    /// an abstract <c>PaymentMethod</c> base mapped to a concrete subtype.
    /// </summary>
    public class ShopFactory : FactoryBase
    {
        protected override void CreateTypeMap()
        {
            // Example: AddTypeMapping(typeof(PaymentMethod), typeof(CreditCardPayment));
        }
    }
}
