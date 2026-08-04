using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace StarterKit.Infrastructure.Services.Notifications;

// public bắt buộc — API's Program.cs gọi MapHub<NotificationHub> bằng type cụ thể, khác cách
// API luôn resolve các channel khác qua INotificationChannel; Infrastructure không có
// InternalsVisibleTo cho StarterKit.API nên không thể internal như EmailNotificationChannel.
[Authorize]
public sealed class NotificationHub : Hub;
