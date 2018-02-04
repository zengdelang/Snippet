using System;
using System.Diagnostics;
using System.IO;

public static class WindowsOSUtility
{
    public static void ExploreDirectory(string path)
    {
        path = path.Replace("/", "\\");
        if (Directory.Exists(path))
        {
            //System.Diagnostics.Process.Start(Path.GetFullPath(path));
            Process open = new Process();
            open.StartInfo.FileName = "explorer";
            open.StartInfo.Arguments = @"/select," + path;
            open.Start();
        }
        else if (File.Exists(path))
        {
            Process open = new Process();
            open.StartInfo.FileName = "explorer";
            open.StartInfo.Arguments = @"/select," + path;
            open.Start();
        }
    }

    public static void OpenFileWithApp(string path)
    {
        path = path.Replace("/", "\\");
        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = "explorer.exe";
        startInfo.Arguments = path;
        try
        {
         
            Process.Start(startInfo);
        }
        catch (Exception e)
        {
            UnityEngine.Debug.LogError(e);
        }
    }
}
