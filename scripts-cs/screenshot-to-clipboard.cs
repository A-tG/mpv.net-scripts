using mpvnet;
using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
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
        try
        {
            ScreenshotToClipboard();
            return true;
        }
        catch { }
        return false;
    }

    private void ScreenshotToClipboard()
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

        var res = (libmpv.mpv_error)mpv_command_node(m_core.Handle, args, resultPtr);
        if (res != libmpv.mpv_error.MPV_ERROR_SUCCESS)
        {
            throw new InvalidOperationException("Command returned error: " + ((int)res).ToString() + " " + res.ToString());
        }

        result = Marshal.PtrToStructure<libmpv.mpv_node>(resultPtr);
        var resultList = Marshal.PtrToStructure<mpv_node_list>(result.list);

        var screenshot = BitmapFromMpvNodeList(resultList);

        libmpv.mpv_free_node_contents(resultPtr);
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
                var PixFormat = PixelFormat.Format24bppRgb;
                bm = new Bitmap((int)w, (int)h, PixFormat);

                var rect = new Rectangle(0, 0, (int)w, (int)h);
                var bmData = bm.LockBits(rect, ImageLockMode.ReadWrite, PixFormat);

                var len = (int)(ba.size.ToUInt64() / 4);
                var readPtr = ba.data;
                var writePtr = bmData.Scan0;
                Parallel.For(0, len, (i) =>
                {
                    var rPtr = readPtr + 4 * i;
                    var wPtr = writePtr + 3 * i;
                    const int gOfs = 1;
                    const int bOfs = 2;
                    Marshal.WriteByte(wPtr, Marshal.ReadByte(rPtr));
                    Marshal.WriteByte(wPtr, gOfs, Marshal.ReadByte(rPtr + gOfs));
                    Marshal.WriteByte(wPtr, bOfs, Marshal.ReadByte(rPtr + bOfs));
                });
                bm.UnlockBits(bmData);
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
