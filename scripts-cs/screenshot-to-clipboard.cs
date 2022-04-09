using mpvnet;
using System;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;

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
        m_core.CommandV("show-text", text, "3000");
        text += TryScreenshotToClipBoard() ?
            ": Succeded" :
            ": Failed";
        m_core.CommandV("show-text", text, "3000");
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
        var fileName = Guid.NewGuid().ToString() + ".png";
        var path = Path.Combine(Path.GetTempPath(), fileName);
        m_core.CommandV("screenshot-to-file", path);
        return path;
    }
}
