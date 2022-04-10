using mpvnet;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

// Add to input.conf
// Ctrl+c script-message screenshot-to-clipboard

class Script
{
    const string Name = "screenshot-to-clipboard"; 

    private CorePlayer m_core = Global.Core;

    public Script()
    {
        m_core.ClientMessage += OnMessage;
    }

    private void OnMessage(string[] args)
    {
        if ((args == null) || (args.Length == 0)) return;

        if (args[0] != Name) return;

        string text = "Copy Screenshot to clipboard";
        m_core.CommandV("show-text", text);
        text += TryScreenshotToClipBoard() ?
            ": Succeded" :
            ": Failed";
        m_core.CommandV("show-text", text);
    }

    public bool TryScreenshotToClipBoard()
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

    private string CreateTempScreenshot()
    {
<<<<<<< Updated upstream
        var fileName = Guid.NewGuid().ToString() + ".png";
        var path = Path.Combine(Path.GetTempPath(), fileName);
        m_core.CommandV("screenshot-to-file", path);
        return path;
=======
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
                var PixFormat = PixelFormat.Format24bppRgb;
                bm = new Bitmap((int)w, (int)h, PixFormat);

                var rect = new Rectangle(0, 0, (int)w, (int)h);
                var bmData = bm.LockBits(rect, ImageLockMode.ReadWrite, PixFormat);

                var len = ba.size.ToUInt64();
                var currReadPtr = ba.data;
                var currWritePtr = bmData.Scan0;
                for (ulong i = 0; i < len; i += 4)
                {
                    var bPtr = currReadPtr;
                    var gPtr = currReadPtr + 1;
                    var rPtr = currReadPtr + 2;
                    Marshal.WriteByte(currWritePtr, Marshal.ReadByte(rPtr));
                    Marshal.WriteByte(currWritePtr, 1, Marshal.ReadByte(gPtr));
                    Marshal.WriteByte(currWritePtr, 2, Marshal.ReadByte(bPtr));
                    currReadPtr = IntPtr.Add(currReadPtr, 4);
                    currWritePtr = IntPtr.Add(currWritePtr, 3);
                }
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
>>>>>>> Stashed changes
    }
}
