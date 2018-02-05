using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;

public class EditorWindowWrap
{
    public delegate bool HasFocusDelegate();
    protected static Dictionary<EditorWindow, HasFocusDelegate> s_ActionMap = new Dictionary<EditorWindow, HasFocusDelegate>();

    public static bool HasFocus(EditorWindow window)
    {
        if (s_ActionMap.ContainsKey(window))
        {
            return s_ActionMap[window]();
        }

        var fi = typeof(EditorWindow).GetField("m_Parent", BindingFlags.NonPublic | BindingFlags.Instance);
        var obj = fi.GetValue(window);
        var p = obj.GetType().GetProperty("hasFocus", BindingFlags.Public | BindingFlags.Instance);

        var action = (HasFocusDelegate)Delegate.CreateDelegate(typeof(HasFocusDelegate), obj, p.GetGetMethod());
        if (action == null)
            throw new NullReferenceException("action");

        s_ActionMap.Add(window, action);
        return action();
    }

    public static void ShowAsDropDown()
    {
        
    }
}
