using System.Collections;
using System.Collections.Generic;
using System.IO;
using Antlr4.Runtime.Misc;
using UnityEngine;

namespace EUTK
{
    public class ItemInfo
    {
        public string[] pathArray;
        public Texture2D[] texture2DArray;
        public object[] infoArray;
    }

    public class ItemPathGenerator
    {
        public static ItemInfo GetItemInfo(string folderPath, string extName)
        {
            if (!Directory.Exists(folderPath))
                return null;

            List<string> pathList = new List<string>();
            List<object> infoList = new List<object>();

            GetCurrentFolderInfo(pathList, infoList, "", folderPath, extName);

            return null;
        }

        /// <summary>
        /// 排序没做
        /// </summary>
        /// <param name="pathList"></param>
        /// <param name="infoList"></param>
        /// <param name="prefix"></param>
        /// <param name="folderPath"></param>
        /// <param name="extName"></param>
        public static void GetCurrentFolderInfo(List<string> pathList, List<object> infoList, string prefix, string folderPath, string extName)
        {
            DirectoryInfo dirInfo = new DirectoryInfo(folderPath);
            foreach (var di in dirInfo.GetDirectories())
            {
                if ((di.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    continue;
                }

                GetCurrentFolderInfo(pathList, infoList, prefix +"/"+di.Name, di.FullName, extName);
            }

            foreach (var fi in dirInfo.GetFiles())
            {
                if ((fi.Attributes & FileAttributes.Hidden) == FileAttributes.Hidden)
                {
                    continue;
                }

                if (extName.ToLower() == fi.Extension.ToLower())
                {
                    Debug.LogError(prefix +"/" + fi.Name);
                    Debug.LogError(prefix + "/" + fi.FullName);
                    break;
                }
            }
        }
    }
}

