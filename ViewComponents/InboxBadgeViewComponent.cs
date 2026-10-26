using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.ViewComponents;

/// <summary>
/// CORE-04 slice 2. The unread counter next to the inbox link in the navigation.
/// </summary>
/// <remarks>
/// One counter for the whole substrate — notifications, chats and plugin threads alike — which is
/// the visible payoff of not building two messaging systems.
/// </remarks>
public sealed class InboxBadgeViewComponent : ViewComponent
{
    private readonly IMessageService _messageService;
    private readonly ICurrentMemberAccessor _currentMember;

    public InboxBadgeViewComponent(IMessageService messageService, ICurrentMemberAccessor currentMember)
    {
        _messageService = messageService;
        _currentMember = currentMember;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var memberId = await _currentMember.GetMemberIdAsync(UserClaimsPrincipal);

        var unread = memberId is null
            ? 0
            : await _messageService.UnreadCountAsync(memberId.Value);

        return View(unread);
    }
}
