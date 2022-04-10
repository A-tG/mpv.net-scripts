using mpvnet;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
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
            var img = GetRawScreenshot();

            var thread = new Thread(() =>
            {
                // need to be done in STA thread
                Clipboard.SetImage(img);
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

    private Bitmap GetRawScreenshot()
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

        var screenshot = BitmapFromMpvNodeList(resultList);

        //libmpv.mpv_free_node_contents(resultPtr);
        Marshal.FreeHGlobal(resultPtr);
        Marshal.FreeHGlobal(commandPtr);
        Marshal.FreeHGlobal(listValPtr);
        Marshal.FreeHGlobal(listPtr);

        return screenshot;
    }
    public Bitmap BitmapFromMpvNodeList(mpv_node_list list)
    {
        Bitmap bm;
        long w, h, stride;
        w = h = stride = 0;
        string format = string.Empty;
        var ba = new mpv_byte_array();

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
                    w = node.int64;
                    break;
                case "h":
                    h = node.int64;
                    break;
                case "stride":
                    stride = node.int64;
                    break;
                case "format":
                    format = Marshal.PtrToStringAnsi(node.str);
                    break;
                case "data":
                    ba = Marshal.PtrToStructure<mpv_byte_array>(node.ba);
                    break;
                default:
                    break;
            }
        }
        switch (format)
        {
            case "bgr0":
                bm = new Bitmap((int)w, (int)h, PixelFormat.Format24bppRgb);
                var currPtr = ba.data;
                var len = ba.size.ToUInt64();
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        var B = Marshal.ReadByte(currPtr);
                        var G = Marshal.ReadByte(currPtr, 1);
                        var R = Marshal.ReadByte(currPtr, 2);
                        var color = Color.FromArgb(R, G, B);
                        bm.SetPixel(x, y, color);
                        currPtr = IntPtr.Add(currPtr, 4);
                    }
                }
                break;
            default:
                throw new ArgumentException("Not supported color format");
        }
        return bm;
    }

    private void OnMessage(string[] args)
    {
        if ((args == null) || (args.Length == 0)) return;

        if (args[0] != Name) return;

        string text = "Copy Screenshot to clipboard";
        m_core.CommandV("show-text", text);

        text += TryScreenshotToClipboard() ?
            ": Succeded" :
            ": Failed";
        m_core.CommandV("show-text", text);
    }
}
