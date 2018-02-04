﻿using UnityEngine;

public class GUIUtilityWrap
{
    public static Rect GUIToScreenRect(Rect guiRect)
    {
        Vector2 vector2 = GUIUtility.GUIToScreenPoint(new Vector2(guiRect.x, guiRect.y));
        guiRect.x = vector2.x;
        guiRect.y = vector2.y;
        return guiRect;
    }
}
