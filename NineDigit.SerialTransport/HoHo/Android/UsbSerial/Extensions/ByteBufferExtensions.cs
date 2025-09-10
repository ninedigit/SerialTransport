#if ANDROID
using Android.Runtime;
using Java.Nio;

namespace Hoho.Android.UsbSerial;

public static class ByteBufferExtensions
{
    public static byte[] ToByteArray(this ByteBuffer buffer)
    {
        IntPtr classHandle = JNIEnv.FindClass("java/nio/ByteBuffer");
        IntPtr methodId = JNIEnv.GetMethodID(classHandle, "array", "()[B");
        IntPtr resultHandle = JNIEnv.CallObjectMethod(buffer.Handle, methodId);

        byte[] result = JNIEnv.GetArray<byte>(resultHandle);

        JNIEnv.DeleteLocalRef(resultHandle);

        return result;
    }
}
#endif