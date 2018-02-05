using System;
using UnityEditorInternal.VR;
using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public enum sss
    {
        xxx,
        yyy,
        zzz
    }

    public int layer;
    public Renderer r;

	void Start () {

	    var type = typeof(UnityEditor.EditorWindow);
        Debug.LogError(type.Assembly.GetType("UnityEditor.ShowMode"));

	    Debug.LogError(type.Assembly.GetType("UnityEditor.PopupLocationHelper+PopupLocation[]"));
	    Debug.LogError(Enum.Parse(type.Assembly.GetType("UnityEditor.ShowMode"), "PopupMenuWithKeyboardFocus"));
    }
	
	// Update is called once per frame
	void Update () {

	    if (Input.GetKeyDown(KeyCode.A))
	    {
	        Debug.LogError(r.sortingOrder);
	        r.sortingOrder = layer;
	        Debug.LogError(r.sortingOrder);
        }

	    var type = typeof(sss[]);
        //Debug.LogError(type);
	}
}
