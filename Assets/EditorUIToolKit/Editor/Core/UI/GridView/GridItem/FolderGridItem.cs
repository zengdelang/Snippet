﻿using UnityEditor;
using UnityEngine;

public class FolderGridItem : GridItem
{
    [SerializeField]
    public string Path;
    [SerializeField]
    public bool IsFolder;
    [SerializeField]
    public int ParentId;

    public override Texture Texture
    {
        get
        {
            return EditorGUIUtility.FindTexture(EditorResourcesUtilityWrap.folderIconName);
        }
    } 
}
