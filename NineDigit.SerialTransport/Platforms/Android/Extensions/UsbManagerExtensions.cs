#if ANDROID
using Android.Content;
using Android.Hardware.Usb;
// ReSharper disable CheckNamespace

namespace NineDigit.SerialTransport
{
    public static class UsbManagerExtensions
    {
        private const string ActionUsbPermission = "com.Hoho.Android.UsbSerial.USB_PERMISSION";

        public static async Task<bool> RequestPermissionAsync(
            this UsbManager manager, UsbDevice device, Context context, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (manager is null)
                throw new ArgumentNullException(nameof(manager));

            if (context is null)
                throw new ArgumentNullException(nameof(context));

            using var usbPermissionReceiver = new UsbPermissionReceiver(cancellationToken);
            using var intentFilter = new IntentFilter(ActionUsbPermission);

            context.RegisterReceiver(usbPermissionReceiver, intentFilter);

            try
            {
                using var intent = new Intent(ActionUsbPermission);
                var pendingIntent = PendingIntent.GetBroadcast(context, 0, intent, 0);
                manager.RequestPermission(device, pendingIntent);

                return await usbPermissionReceiver.Task
                    .ConfigureAwait(false);
            }
            catch (Exception)
            {
                context.UnregisterReceiver(usbPermissionReceiver);
                throw;
            }
        }

        private class UsbPermissionReceiver : BroadcastReceiver
        {
            private readonly CancellationTokenRegistration _cancellationTokenRegistration;
            private readonly TaskCompletionSource<bool> _completionSource;

            private bool _disposed;

            public UsbPermissionReceiver(CancellationToken cancellationToken)
            {
                _cancellationTokenRegistration = cancellationToken.Register(OnCanceled);
                _completionSource = new TaskCompletionSource<bool>();
            }

            public Task<bool> Task
                => _completionSource.Task;

            private void OnCanceled()
                => _completionSource.TrySetCanceled();

            public override void OnReceive(Context? context, Intent? intent)
            {
                // var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;

                if (intent != null)
                {
                    var permissionGranted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
                    _completionSource.TrySetResult(permissionGranted);
                }

                context?.UnregisterReceiver(this);
            }

            protected override void Dispose(bool disposing)
            {
                if (_disposed)
                    return;

                if (disposing)
                    _cancellationTokenRegistration.Dispose();

                _disposed = true;

                base.Dispose(disposing);
            }
        }
    }
}
#endif
