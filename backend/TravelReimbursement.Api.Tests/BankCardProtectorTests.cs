using Microsoft.AspNetCore.DataProtection;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class BankCardProtectorTests
{
    [Fact]
    public void Protected_bank_card_round_trips_without_storing_plaintext()
    {
        const string bankCardNumber = "6222021234567890";
        var protector = new BankCardProtector(new EphemeralDataProtectionProvider());

        var protectedValue = protector.Protect(bankCardNumber);

        Assert.NotEqual(bankCardNumber, protectedValue);
        Assert.DoesNotContain(bankCardNumber, protectedValue, StringComparison.Ordinal);
        Assert.Equal(bankCardNumber, protector.Unprotect(protectedValue));
    }
}
