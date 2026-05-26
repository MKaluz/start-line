namespace StartLine.Application.Payments;

public record PaymentResult(bool Success, string? ErrorMessage = null);

/// <summary>Abstraction over the payment provider. The mock implementation always succeeds.</summary>
public interface IPaymentProvider
{
    Task<PaymentResult> ProcessPaymentAsync(Guid registrationId, CancellationToken ct = default);
}
