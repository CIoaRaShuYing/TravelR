using Microsoft.AspNetCore.DataProtection;

namespace TravelReimbursement.Api.Services;

public interface IBankCardProtector
{
    string Protect(string bankCardNumber);
    string Unprotect(string protectedValue);
}

public sealed class BankCardProtector(IDataProtectionProvider provider) : IBankCardProtector
{
    private readonly IDataProtector protector = provider.CreateProtector("TravelReimbursement.BankCard.v1");

    public string Protect(string bankCardNumber) => protector.Protect(bankCardNumber);

    public string Unprotect(string protectedValue) => protector.Unprotect(protectedValue);
}

public static class MealAllowanceCalculator
{
    public static int CalculateDays(DateOnly? departureDate, DateOnly? returnDate)
    {
        if (!departureDate.HasValue || !returnDate.HasValue || returnDate.Value < departureDate.Value) return 0;
        return returnDate.Value.DayNumber - departureDate.Value.DayNumber + 1;
    }
}
