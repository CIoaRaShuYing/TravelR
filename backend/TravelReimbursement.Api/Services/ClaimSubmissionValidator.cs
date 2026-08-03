using TravelReimbursement.Api.Domain;

namespace TravelReimbursement.Api.Services;

public static class ClaimSubmissionValidator
{
    public static Dictionary<string, string[]> Validate(ClaimType type, ClaimVersion version)
    {
        var errors = new Dictionary<string, string[]>();
        if (string.IsNullOrWhiteSpace(version.Description)) errors["description"] = ["报销说明不能为空。"];
        if (version.ExpenseItems.Count == 0) errors["expenseItems"] = ["至少需要一项费用。"];
        if (version.ExpenseItems.Any(x => x.Category == ExpenseCategory.Unspecified)) errors["category"] = ["每项费用均需选择类别。"];
        if (version.ExpenseItems.Any(x => x.Amount is null or <= 0)) errors["amount"] = ["每项费用金额必须大于零。"];
        if (version.ExpenseItems.Any(x => x.ExpenseDate is null)) errors["expenseDate"] = ["每项费用均需填写费用日期。"];
        if (version.ExpenseItems.Any(x => string.IsNullOrWhiteSpace(x.Merchant))) errors["merchant"] = ["每项费用均需填写商户或承运方。"];
        if (version.ExpenseItems.Any(x => x.AttachmentLinks.All(link => link.AttachmentAsset.ScanStatus != AttachmentScanStatus.Accepted)))
            errors["attachments"] = ["每项费用均需上传有效凭证。"];

        if (type == ClaimType.Travel)
        {
            var itinerary = version.TravelItinerary;
            if (itinerary is null
                || string.IsNullOrWhiteSpace(itinerary.DepartureLocation)
                || string.IsNullOrWhiteSpace(itinerary.Destination)
                || itinerary.DepartureDate is null
                || itinerary.ReturnDate is null)
            {
                errors["travelItinerary"] = ["差旅行程信息必须填写完整。"];
            }
            else if (itinerary.ReturnDate < itinerary.DepartureDate)
            {
                errors["returnDate"] = ["返回日期不能早于出发日期。"];
            }

            if (!version.ExpenseItems.Any(x => x.Category == ExpenseCategory.DepartureTransport))
                errors["departureTransport"] = ["请至少录入一项去程交通凭证。"];
            if (!version.ExpenseItems.Any(x => x.Category == ExpenseCategory.ReturnTransport))
                errors["returnTransport"] = ["请至少录入一项回程交通凭证。"];
        }

        return errors;
    }
}
