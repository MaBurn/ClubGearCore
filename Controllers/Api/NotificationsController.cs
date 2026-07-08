using ClubGear.Services.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ClubGear.Controllers.Api;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
    {
        _notificationService = notificationService;
    }

    [HttpPost("send-test")]
    public async Task<IActionResult> SendTest([FromBody] SendNotificationRequest request, CancellationToken cancellationToken)
    {
        var result = await _notificationService.NotifyAsync(new NotificationMessage(
            Recipient: request.Recipient,
            Subject: request.Subject,
            Body: request.Body,
            Channel: request.Channel,
            CorrelationId: "api-test"), cancellationToken);

        if (!result.Success)
        {
            return BadRequest(result);
        }

        return Ok(result);
    }
}

public sealed record SendNotificationRequest(string Recipient, string Subject, string Body, string Channel = "email");
