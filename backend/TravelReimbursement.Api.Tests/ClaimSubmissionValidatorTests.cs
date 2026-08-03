using TravelReimbursement.Api.Domain;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class ClaimSubmissionValidatorTests
{
    [Fact]
    public void Travel_claim_with_departure_and_return_but_no_lodging_is_valid()
    {
        var version = CreateVersion(
            CreateItem(ExpenseCategory.DepartureTransport),
            CreateItem(ExpenseCategory.ReturnTransport));
        version.TravelItinerary = new TravelItinerary
        {
            DepartureLocation = "上海",
            Destination = "杭州",
            DepartureDate = new DateOnly(2026, 7, 30),
            ReturnDate = new DateOnly(2026, 7, 30)
        };

        var errors = ClaimSubmissionValidator.Validate(ClaimType.Travel, version);

        Assert.Empty(errors);
    }

    [Fact]
    public void Incomplete_draft_is_rejected_only_when_submitting()
    {
        var version = new ClaimVersion { Description = string.Empty };

        var errors = ClaimSubmissionValidator.Validate(ClaimType.Travel, version);

        Assert.Contains("description", errors.Keys);
        Assert.Contains("expenseItems", errors.Keys);
        Assert.Contains("travelItinerary", errors.Keys);
    }

    [Fact]
    public void Travel_claim_without_return_transport_is_rejected()
    {
        var version = CreateVersion(CreateItem(ExpenseCategory.DepartureTransport));
        version.TravelItinerary = new TravelItinerary
        {
            DepartureLocation = "上海",
            Destination = "杭州",
            DepartureDate = new DateOnly(2026, 7, 30),
            ReturnDate = new DateOnly(2026, 7, 30)
        };

        var errors = ClaimSubmissionValidator.Validate(ClaimType.Travel, version);

        Assert.Contains("returnTransport", errors.Keys);
    }

    [Fact]
    public void General_claim_does_not_require_travel_categories()
    {
        var version = CreateVersion(CreateItem(ExpenseCategory.OfficeSupplies));

        var errors = ClaimSubmissionValidator.Validate(ClaimType.General, version);

        Assert.Empty(errors);
    }

    [Fact]
    public void Expense_category_must_be_selected_before_submitting()
    {
        var version = CreateVersion(CreateItem(ExpenseCategory.Unspecified));

        var errors = ClaimSubmissionValidator.Validate(ClaimType.General, version);

        Assert.Contains("category", errors.Keys);
    }

    [Fact]
    public void Every_expense_item_requires_an_accepted_attachment()
    {
        var item = CreateItem(ExpenseCategory.OfficeSupplies);
        item.AttachmentLinks.Clear();
        var version = CreateVersion(item);

        var errors = ClaimSubmissionValidator.Validate(ClaimType.General, version);

        Assert.Contains("attachments", errors.Keys);
    }

    [Fact]
    public void Return_date_cannot_be_before_departure_date()
    {
        var version = CreateVersion(
            CreateItem(ExpenseCategory.DepartureTransport),
            CreateItem(ExpenseCategory.ReturnTransport));
        version.TravelItinerary = new TravelItinerary
        {
            DepartureLocation = "上海",
            Destination = "杭州",
            DepartureDate = new DateOnly(2026, 8, 2),
            ReturnDate = new DateOnly(2026, 8, 1)
        };

        var errors = ClaimSubmissionValidator.Validate(ClaimType.Travel, version);

        Assert.Contains("returnDate", errors.Keys);
    }

    private static ClaimVersion CreateVersion(params ExpenseItem[] items) => new()
    {
        Description = "测试报销说明",
        ExpenseItems = items.ToList()
    };

    private static ExpenseItem CreateItem(ExpenseCategory category) => new()
    {
        ClientKey = Guid.NewGuid(),
        Category = category,
        Amount = 100m,
        ExpenseDate = new DateOnly(2026, 7, 30),
        Merchant = "测试商户",
        AttachmentLinks =
        [
            new ExpenseItemAttachment
            {
                AttachmentAsset = new AttachmentAsset { ScanStatus = AttachmentScanStatus.Accepted }
            }
        ]
    };
}
