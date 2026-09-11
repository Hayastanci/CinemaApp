#if ANDROID
using Android.App;
using Android.Content;
using Android.OS;
using AndroidX.Core.App;
using AndroidX.Core.Content;
#endif

namespace CinemaApp.Mobile.Services;

public class MobileNotificationService
{
    private const string ChannelId = "cinema_support_channel";
    private const string ChannelName = "Support & Helpdesk";
    private const string ChannelDescription = "Notifications for support ticket updates, staff replies, and assistance.";

    public MobileNotificationService()
    {
        InitializeChannel();
    }

    public void RequestNotificationPermission()
    {
#if ANDROID
        try
        {
            if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
            {
                var activity = Microsoft.Maui.ApplicationModel.Platform.CurrentActivity;
                if (activity != null)
                {
                    if (ContextCompat.CheckSelfPermission(activity, Android.Manifest.Permission.PostNotifications) != Android.Content.PM.Permission.Granted)
                    {
                        ActivityCompat.RequestPermissions(activity, new[] { Android.Manifest.Permission.PostNotifications }, 1001);
                        System.Diagnostics.Debug.WriteLine("[Notification] Requested POST_NOTIFICATIONS permission on Android 13+");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] RequestNotificationPermission failed: {ex.Message}");
        }
#endif
    }

    private void InitializeChannel()
    {
#if ANDROID
        if (Build.VERSION.SdkInt >= BuildVersionCodes.O)
        {
            var context = Android.App.Application.Context;
            var notificationManager = (NotificationManager?)context.GetSystemService(Context.NotificationService);
            if (notificationManager != null)
            {
                var channel = new NotificationChannel(ChannelId, ChannelName, NotificationImportance.High)
                {
                    Description = ChannelDescription,
                    LockscreenVisibility = NotificationVisibility.Public
                };
                channel.EnableVibration(true);
                channel.EnableLights(true);
                channel.SetShowBadge(true);
                notificationManager.CreateNotificationChannel(channel);
            }
        }
#endif
    }

    public void ShowTicketNotification(int ticketId, string title, string shortMessage)
    {
        RequestNotificationPermission();

#if ANDROID
        try
        {
            var context = Android.App.Application.Context;
            var intent = context.PackageManager?.GetLaunchIntentForPackage(context.PackageName ?? "");
            var pendingIntent = intent != null 
                ? PendingIntent.GetActivity(context, ticketId, intent, PendingIntentFlags.UpdateCurrent | PendingIntentFlags.Immutable)
                : null;

            int iconId = Android.Resource.Drawable.IcDialogInfo;
            try
            {
                if (context.ApplicationInfo != null && context.ApplicationInfo.Icon != 0)
                {
                    iconId = context.ApplicationInfo.Icon;
                }
            }
            catch { }

            var builder = new NotificationCompat.Builder(context, ChannelId)
                .SetSmallIcon(iconId)
                .SetContentTitle(title)
                .SetContentText(shortMessage)
                .SetStyle(new NotificationCompat.BigTextStyle().BigText(shortMessage))
                .SetPriority(NotificationCompat.PriorityHigh)
                .SetCategory(NotificationCompat.CategoryMessage)
                .SetDefaults((int)(NotificationDefaults.Sound | NotificationDefaults.Vibrate))
                .SetAutoCancel(true);

            if (pendingIntent != null)
            {
                builder.SetContentIntent(pendingIntent);
            }

            var manager = NotificationManagerCompat.From(context);
            var notifyId = (ticketId * 37 + (System.Environment.TickCount & 0xFFF)) & 0x7FFFFFFF;
            manager.Notify(notifyId, builder.Build());
            System.Diagnostics.Debug.WriteLine($"[Notification] Android push notification posted for ticket #{ticketId} (notifyId {notifyId}): {title} - {shortMessage}");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[Notification] Failed to show Android notification: {ex.Message}");
        }
#else
        System.Diagnostics.Debug.WriteLine($"[Notification] Push notification ({ticketId}): {title} - {shortMessage}");
#endif
    }
}
