#if ANDROID
/* Copyright 2017 Tyler Technologies Inc.
 *
 * Project home page: https://github.com/anotherlab/xamarin-usb-serial-for-android
 * Portions of this library are based on usb-serial-for-android (https://github.com/mik3y/usb-serial-for-android).
 * Portions of this library are based on Xamarin USB Serial for Android (https://bitbucket.org/lusovu/xamarinusbserial).
 */

using Android.Util;
using Hoho.Android.UsbSerial.Drivers;

namespace Hoho.Android.UsbSerial;

public class ProbeTable
{
    public static readonly ProbeTable Default = CreateDefaultProbeTable();
    
    private const string Tag = nameof(ProbeTable);

    private readonly Dictionary<Tuple<int, int>, Type> _probeTable = new();

    /**
     * Adds or updates a (vendor, product) pair in the table.
     *
     * @param vendorId the USB vendor id
     * @param productId the USB product id
     * @param driverClass the driver class responsible for this pair
     * @return {@code this}, for chaining
     */
    public ProbeTable AddProduct(int vendorId, int productId, Type driverClass)
    {
        var key = new Tuple<int, int>(vendorId, productId);
        _probeTable.TryAdd(key, driverClass);
        return this;
    }

    public ProbeTable AddDriver<TDriver>(ImmutableDeviceList deviceList)
        where TDriver : IUsbSerialDriver
        => AddDriver(typeof(TDriver), deviceList);
    
    public ProbeTable AddDriver(Type driverClass, ImmutableDeviceList deviceList)
    {
        ArgumentNullException.ThrowIfNull(driverClass);
        
        if (!driverClass.IsAssignableTo(typeof(IUsbSerialDriver)))
            throw new ArgumentException($"Expecting driver of type {typeof(IUsbSerialDriver).FullName}");
        
        foreach (var entry in deviceList)
        {
            try
            {
                AddProduct(entry.VendorId, entry.ProductId, driverClass);
                Log.Debug(Tag, $"Added {entry.VendorId:X}, {entry.ProductId:X}, {driverClass}");
            }
            catch (Exception)
            {
                Log.Debug(Tag, $"Error adding {entry.VendorId:X}, {entry.ProductId:X}, {driverClass}");
                throw;
            }
        }

        return this;
    }

    public Type? FindDriver(int vendorId, int productId)
    {
        var pair = new Tuple<int, int>(vendorId, productId);
        var result = _probeTable.GetValueOrDefault(pair);

        return result;
    }
    
    private static ProbeTable CreateDefaultProbeTable()
    {
        var probeTable = new ProbeTable();
        
        probeTable.AddDriver<CdcAcmSerialDriver>(CdcAcmSerialDriver.GetSupportedDevices());
        probeTable.AddDriver<Cp21xxSerialDriver>(Cp21xxSerialDriver.GetSupportedDevices());
        probeTable.AddDriver<FtdiSerialDriver>(FtdiSerialDriver.GetSupportedDevices());
        probeTable.AddDriver<ProlificSerialDriver>(ProlificSerialDriver.GetSupportedDevices());
        probeTable.AddDriver<Ch34xSerialDriver>(Ch34xSerialDriver.GetSupportedDevices());
        probeTable.AddDriver<STM32SerialDriver>(STM32SerialDriver.GetSupportedDevices());
        
        return probeTable;
    }
}
#endif