﻿using UnityEngine;

public class FileGridItem : FolderGridItem
{
    public override Texture Texture
    {
        get
        {
            return Resources.Load<Texture>("config");
        }
    }
}
