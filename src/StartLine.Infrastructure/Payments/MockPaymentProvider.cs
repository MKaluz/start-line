using StartLine.Application.Payments;

namespace StartLine.Infrastructure.Payments;

/// <summary>Mock payment provider that always succeeds. Used until a real payment gateway is integrated.</summary>
public class MockPaymentProvider : IPaymentProvider
{
    public Task<PaymentResult> ProcessPaymentAsync(Guid registrationId, CancellationToken ct = default) =>
        Task.FromResult(new PaymentResult(Success: true));
}
