using TravelReimbursement.Api.Contracts;
using TravelReimbursement.Api.Services;

namespace TravelReimbursement.Api.Tests;

public sealed class MeetingRecordServiceTests
{
    [Fact]
    public void Normalize_keeps_meeting_items_in_one_ordered_list()
    {
        var input = MeetingRecordService.Normalize(
            Guid.NewGuid(),
            new DateOnly(2026, 9, 3),
            "  第一会议室  ",
            [new MeetingParticipantRequest(" 张三 ", null, null, null)],
            [
                new MeetingRecordItemRequest(" 需求确认 ", " 进行中 ", null, " 李四 "),
                new MeetingRecordItemRequest(" 联调安排 ", null, new DateOnly(2026, 9, 8), null)
            ]);

        Assert.Equal("第一会议室", input.Location);
        Assert.Equal(["需求确认", "联调安排"], input.Items.Select(item => item.Content));
        Assert.Equal("进行中", input.Items[0].Status);
        Assert.Equal("李四", input.Items[0].Owner);
    }
}
