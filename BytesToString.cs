using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

public class BytesToString
{
    [MenuItem("Tools/ConvertBytesToString")]
    public static void ConvertBytes()
    {
        var path = EditorUtility.OpenFilePanel("", "", "7z");
        var data = File.ReadAllBytes(path);
        var str = Convert.ToBase64String(data);
        

        int i = 0;
        int r = 0;
        var sb = new StringBuilder();

        foreach (var c in str)
        {
            sb.Append(c);
            ++i;
            if (i == 64)
            {
                i = 0;
                sb.Append("\r\n");

                ++r;
                if (r == 10)
                {
                    r = 0;
                    sb.Append("\r\n");
                }
            }
        }

        File.WriteAllText("D:/test.txt", sb.ToString());
    }
}
