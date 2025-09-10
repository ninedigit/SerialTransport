#if ANDROID
using Android.Hardware.Usb;
using Hoho.Android.UsbSerial.Drivers;
using Java.Lang;
using Java.Lang.Reflect;

namespace Hoho.Android.UsbSerial;

public class UsbSerialProber
{
    public static readonly UsbSerialProber Default = new(ProbeTable.Default);
    
    private readonly ProbeTable _probeTable;

    public UsbSerialProber(ProbeTable probeTable)
    {
        ArgumentNullException.ThrowIfNull(probeTable);
        _probeTable = probeTable;
    }

    /**
     * Finds and builds all possible {@link UsbSerialDriver UsbSerialDrivers}
     * from the currently-attached {@link UsbDevice} hierarchy. This method does
     * not require permission from the Android USB system, since it does not
     * open any of the devices.
     *
     * @param usbManager
     * @return a list, possibly empty, of all compatible drivers
     */
    public List<IUsbSerialDriver> FindAllDrivers(UsbManager usbManager)
    {
        var result = new List<IUsbSerialDriver>();

        foreach (var usbDevice in usbManager.DeviceList?.Values ?? Enumerable.Empty<UsbDevice>())
        {
            var driver = ProbeDevice(usbDevice);
            if (driver != null)
            {
                result.Add(driver);
            }
        }
        
        return result;
    }

    /// <summary>
    /// Probes a single device for a compatible driver.
    /// </summary>
    /// <param name="usbDevice">The usb device to probe</param>
    /// <returns>Returns a new <see cref="UsbSerialDriverBase" /> compatible with this device, or <c>null</c>
    /// if none available</returns>
    /// <exception cref="RuntimeException"></exception>
    public IUsbSerialDriver? ProbeDevice(UsbDevice usbDevice)
    {
        var vendorId = usbDevice.VendorId;
        var productId = usbDevice.ProductId;

        var driverClass = _probeTable.FindDriver(vendorId, productId);

        if (driverClass == null)
            return null;
        
        IUsbSerialDriver driver;
        
        try
        {
            driver = (IUsbSerialDriver)Activator.CreateInstance(driverClass, usbDevice)!;
        } catch (NoSuchMethodException e) {
            throw new RuntimeException(e);
        } catch (IllegalArgumentException e) {
            throw new RuntimeException(e);
        } catch (InstantiationException e) {
            throw new RuntimeException(e);
        } catch (IllegalAccessException e) {
            throw new RuntimeException(e);
        } catch (InvocationTargetException e) {
            throw new RuntimeException(e);
        }
        return driver;
    }
}
#endif