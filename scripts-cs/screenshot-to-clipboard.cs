using mpvnet;
using System;
using System.Drawing;
using System.IO;
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
    internal static extern int mpv_command_node(IntPtr ctx, libmpv.mpv_node args, libmpv.mpv_node result);

    private void GetRawScreenshot()
    {
        var args = new libmpv.mpv_node();
        var result = new libmpv.mpv_node();

        var list = new mpv_node_list();
        var listValues = new libmpv.mpv_node();
        
        listValues.format = libmpv.mpv_format.MPV_FORMAT_STRING;
        IntPtr commandPtr = Marshal.StringToHGlobalAnsi("screenshot-raw");
        listValues.str = commandPtr;

        var listValPtr = Marshal.AllocHGlobal(Marshal.SizeOf(listValues));
        Marshal.StructureToPtr(listValues, listValPtr, false);
        list.values = listValPtr;

        var listPtr = Marshal.AllocHGlobal(Marshal.SizeOf(list));
        Marshal.StructureToPtr(list, listPtr, false);
        list.num = 1;
        args.list = listPtr;
        args.format = libmpv.mpv_format.MPV_FORMAT_NODE_ARRAY;

        var res = mpv_command_node(m_core.Handle, args, result);
        m_core.CommandV("show-text", res.ToString());

        Marshal.FreeHGlobal(commandPtr);
        Marshal.FreeHGlobal(listValPtr);
        Marshal.FreeHGlobal(listPtr);
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
