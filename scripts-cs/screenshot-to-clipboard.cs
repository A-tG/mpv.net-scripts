using mpvnet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;

// Add to input.conf
// Ctrl+c script-message screenshot-to-clipboard

[StructLayout(LayoutKind.Sequential)]
struct mpv_node_list
{
    public int num;
    public IntPtr values;
    public IntPtr keys;
}

[StructLayout(LayoutKind.Sequential)]
struct mpv_byte_array
{
    public IntPtr data;
    public UIntPtr size;
}

class MpvScreenshotData
{
    public long w, h, stride;
    public string format = string.Empty;
    public byte[] data = Array.Empty<byte>();

    static public MpvScreenshotData FromMpvNodeList(mpv_node_list list)
    {
        var screenshotData = new MpvScreenshotData();

        for (int i = 0; i < list.num; i++)
        {
            int keyOffset = i * IntPtr.Size;
            int nodeOffset = i * Marshal.SizeOf<libmpv.mpv_node>();

            var ptrVal = Marshal.ReadInt64(list.keys + keyOffset);

            string key = Marshal.PtrToStringAnsi(new IntPtr(ptrVal));
            var node = Marshal.PtrToStructure<libmpv.mpv_node>(list.values + nodeOffset);

            switch (key)
            {
                case "w":
                    screenshotData.w = node.int64;
                    break;
                case "h":
                    screenshotData.h = node.int64;
                    break;
                case "stride":
                    screenshotData.stride = node.int64;
                    break;
                case "format":
                    screenshotData.format = Marshal.PtrToStringAnsi(node.str);
                    break;
                case "data":
                    var ba = Marshal.PtrToStructure<mpv_byte_array>(node.ba);
                    screenshotData.data = UnmanagedArrToArr(ba.data, ba.size.ToUInt64());
                    break;
                default:
                    break;
            }
        }
        return screenshotData;
    }

    private static byte[] UnmanagedArrToArr(IntPtr ptr, ulong len)
    {
        var arr = new byte[len];
        var currentPtr = ptr;
        for (ulong i = 0; i < len; i++)
        {
            arr[i] = Marshal.ReadByte(currentPtr);
            currentPtr = IntPtr.Add(currentPtr, 1);
        }
        return arr;
    }
}

class Script
{
    const string Name = "screenshot-to-clipboard"; 

    private CorePlayer m_core = Global.Core;

    public Script()
    {
        m_core.ClientMessage += OnMessage;
    }

    public bool TryScreenshotToClipboard()
    {
        bool result = false;
        try
        {
            var path = CreateTempScreenshot();

            var thread = new Thread(() =>
            {
                using (var img = new Bitmap(path))
                {
                    // need to be done in STA thread
                    Clipboard.SetImage(img);
                }
                File.Delete(path);
            });
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();

            thread.Join();
            result = true;
        }
        catch { }
        return result;
    }

    [DllImport("mpv-2.dll")]
    internal static extern int mpv_command_node(IntPtr ctx, libmpv.mpv_node args, IntPtr result);

    private MpvScreenshotData GetRawScreenshot()
    {
        var args = new libmpv.mpv_node();
        var result = new libmpv.mpv_node();

        var list = new mpv_node_list();
        var listValues = new libmpv.mpv_node();
        
        IntPtr commandPtr = Marshal.StringToHGlobalAnsi("screenshot-raw");
        listValues.str = commandPtr;
        listValues.format = libmpv.mpv_format.MPV_FORMAT_STRING;

        var listValPtr = Marshal.AllocHGlobal(Marshal.SizeOf(listValues));
        Marshal.StructureToPtr(listValues, listValPtr, false);
        list.values = listValPtr;

        var listPtr = Marshal.AllocHGlobal(Marshal.SizeOf(list));
        list.num = 1;
        args.list = listPtr;
        args.format = libmpv.mpv_format.MPV_FORMAT_NODE_ARRAY;
        Marshal.StructureToPtr(list, listPtr, false);

        var resultPtr = Marshal.AllocHGlobal(Marshal.SizeOf(result));
        Marshal.StructureToPtr(result, resultPtr, false);

        var res = mpv_command_node(m_core.Handle, args, resultPtr);

        result = Marshal.PtrToStructure<libmpv.mpv_node>(resultPtr);
        var resultList = Marshal.PtrToStructure<mpv_node_list>(result.list);

        var screenshot = MpvScreenshotData.FromMpvNodeList(resultList);

        //libmpv.mpv_free_node_contents(resultPtr);
        Marshal.FreeHGlobal(resultPtr);
        Marshal.FreeHGlobal(commandPtr);
        Marshal.FreeHGlobal(listValPtr);
        Marshal.FreeHGlobal(listPtr);

        return screenshot;
    }

    private void OnMessage(string[] args)
    {
        if ((args == null) || (args.Length == 0)) return;

        if (args[0] != Name) return;

        var scr = GetRawScreenshot();
        m_core.CommandV("show-text", scr.stride.ToString()); return;

        string text = "Copy Screenshot to clipboard";
        m_core.CommandV("show-text", text);

        text += TryScreenshotToClipboard() ?
            ": Succeded" :
            ": Failed";
        m_core.CommandV("show-text", text);
    }

    private string CreateTempScreenshot()
    {
        var fileName = Guid.NewGuid().ToString() + ".png";
        var path = Path.Combine(Path.GetTempPath(), fileName);
        m_core.CommandV("screenshot-to-file", path);
        return path;
    }
}
