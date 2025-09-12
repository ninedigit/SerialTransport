// #if ANDROID
// /* Copyright 2017 Tyler Technologies Inc.
//  *
//  * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
//  * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
//  * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
//  */
//
// using Android.Hardware.Usb;
// using Android.Content;
// using Android.OS;
//
// namespace Hoho.Android.UsbSerial
// {
//     public static class UsbManagerExtensions
//     {
//         private const string ActionUsbPermission = "com.Hoho.Android.UsbSerial.Util.USB_PERMISSION";
//
//         //static readonly Dictionary<Tuple<Context, UsbDevice>, TaskCompletionSource<bool>> taskCompletionSources =
//         //    new Dictionary<Tuple<Context, UsbDevice>, TaskCompletionSource<bool>>();
//
//         public static Task<bool> RequestPermissionAsync(this UsbManager manager, UsbDevice device, Context context)
//         {
//             var completionSource = new TaskCompletionSource<bool>();
//
//             var usbPermissionReceiver = new UsbPermissionReceiver(completionSource);
//             if (Build.VERSION.SdkInt >= BuildVersionCodes.Tiramisu)
//             {
// #pragma warning disable CA1416
//                 context.RegisterReceiver(usbPermissionReceiver, new IntentFilter(ActionUsbPermission), ReceiverFlags.NotExported);
// #pragma warning restore CA1416
//             }
//             else
//             {
//                 context.RegisterReceiver(usbPermissionReceiver, new IntentFilter(ActionUsbPermission));
//             }
//
//             // Targeting S+ (version 31 and above) requires that one of FLAG_IMMUTABLE or FLAG_MUTABLE be specified when creating a PendingIntent.
// #if NET6_0_OR_GREATER
//             PendingIntentFlags pendingIntentFlags = Build.VERSION.SdkInt >= BuildVersionCodes.S ? PendingIntentFlags.Mutable : 0;
// #else
//             PendingIntentFlags pendingIntentFlags = Build.VERSION.SdkInt >= (BuildVersionCodes)31 ? (PendingIntentFlags)33554432 : 0;
// #endif
//
//             var intent = new Intent(ActionUsbPermission);
//             if (Build.VERSION.SdkInt >= BuildVersionCodes.UpsideDownCake)
//             {
//                 intent.SetPackage(context.PackageName);
//             }
//             var pendingIntent = PendingIntent.GetBroadcast(context, 0, intent, pendingIntentFlags);
//
//             manager.RequestPermission(device, pendingIntent);
//
//             return completionSource.Task;
//         }
//
//         private class UsbPermissionReceiver : BroadcastReceiver
//         {
//             private readonly TaskCompletionSource<bool> _completionSource;
//
//             public UsbPermissionReceiver(TaskCompletionSource<bool> completionSource)
//             {
//                 _completionSource = completionSource;
//             }
//
//             public override void OnReceive(Context? context, Intent? intent)
//             {
//                 if (intent is null || context is null)
//                     return;
//                 
//                 var device = intent.GetParcelableExtra(UsbManager.ExtraDevice) as UsbDevice;
//                 var permissionGranted = intent.GetBooleanExtra(UsbManager.ExtraPermissionGranted, false);
//                 context.UnregisterReceiver(this);
//                 _completionSource.TrySetResult(permissionGranted);
//             }
//         }
//     }
// }
// #endif