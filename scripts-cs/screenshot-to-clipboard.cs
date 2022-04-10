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

class Script
{
    const string Name = "screenshot-to-clipboard"; 

    private CorePlayer m_core = Global.Core;

    public Script()
    {
        m_core.ClientMessage += OnMessage;
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

    [DllImport("mpv-2.dll")]
    internal static extern int mpv_command_node(IntPtr ctx, libmpv.mpv_node args, IntPtr result);

    private void GetRawScreenshot()
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

        result = (libmpv.mpv_node)Marshal.PtrToStructure(resultPtr, typeof(libmpv.mpv_node));
        var resultList = Marshal.PtrToStructure<mpv_node_list>(result.list);
        GetBitmapFromMpvNodeList(resultList);

        //libmpv.mpv_free_node_contents(resultPtr);
        Marshal.FreeHGlobal(resultPtr);
        Marshal.FreeHGlobal(commandPtr);
        Marshal.FreeHGlobal(listValPtr);
        Marshal.FreeHGlobal(listPtr);
    }

    private void GetBitmapFromMpvNodeList(mpv_node_list list)
    {
        var len = list.num;
        var dict = new Dictionary<string, IntPtr>(len);
        var arr = new string[len];
        for (int i = 0; i < len; i++)
        {
            var ptrVal = Marshal.ReadInt64(list.keys + i);
            var key = Marshal.PtrToStringAnsi(new IntPtr(ptrVal));
            //dict.Add(key, list.values + i);
            arr[i] = key;
        }
        m_core.CommandV("show-text", string.Join(",", arr)); // PROBLEM: prints w,,,, all strings are empty except first
    }

    private void OnMessage(string[] args)
    {
        if ((args == null) || (args.Length == 0)) return;

        if (args[0] != Name) return;

        GetRawScreenshot(); return;
        string text = "Copy Screenshot to clipboard";
        m_core.CommandV("show-text", text);

        text += TryScreenshotToClipBoard() ?
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
