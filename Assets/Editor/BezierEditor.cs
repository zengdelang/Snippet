using EUTK;
using UnityEngine;
using UnityEditor;


//CustomEditor即将该编辑代码附加在Bezier.cs上  
//当Bezier.cs绑定物体被选中时  
//该代码会运行  
[CustomEditor(typeof(Bezier))]
public class BezierEditor : Editor
{
    private static Bezier bezier;


    //每次激活时会运行  
    void OnEnable()
    {
        //target表示目前inspector内显示物体  
        bezier = target as Bezier;
    }


    void OnSceneGUI()
    {
        //创建一个可自由拖曳的点  
        bezier.startPosition = Handles.PositionHandle(bezier.startPosition, Quaternion.identity);
        //创建一个可自由拖曳的控制轴，这里可以把其当成Tangent，即切线  
        bezier.startTangent = Handles.FreeMoveHandle(bezier.startTangent, Quaternion.identity, 1F, Vector3.zero, Handles.SphereCap);
        //这里画条线是为了更加直观表示  
        Handles.DrawLine(bezier.startPosition, bezier.startTangent);
        bezier.endPosition = Handles.PositionHandle(bezier.endPosition, Quaternion.identity);
        bezier.endTangent = Handles.FreeMoveHandle(bezier.endTangent, Quaternion.identity, 1F, Vector3.zero, Handles.SphereCap);
        Handles.DrawLine(bezier.endPosition, bezier.endTangent);


        Handles.DrawBezier(bezier.startPosition, bezier.endPosition, bezier.startTangent, bezier.endTangent, Color.green, null, 10F);
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        /*GUIContent label = new GUIContent("设置");
        Rect rect = GUILayoutUtility.GetRect(label, "Button"); 
        if (GUI.Button(rect, label))
        {
            //var deltaX = (rect.width - 230) / 2;
            //rect.x += deltaX;
            //rect.width = 230;
            if (AddItemWindow.Show(rect, "ssss"))
            {
                AddItemWindow.s_AddItemWindow.Init(rect,
                    ItemPathGenerator.GetItemInfo("D:\\GitHub\\test\\Assets", ".txt").pathArray, null, null, null);
            }
        }*/

        EditorGUILayout.BeginHorizontal();
        GUILayout.FlexibleSpace();
        GUIContent addComponentLabel = new GUIContent("Add Component");
        Rect rect = GUILayoutUtility.GetRect(addComponentLabel, "AC Button");
        if (EditorGUI.DropdownButton(rect, addComponentLabel, FocusType.Passive, "AC Button") && AddItemWindow.Show(rect, "ssss"))
        {
            //初始化不能放在Show里面，GetItemInfo的操作可能超过50ms，这样再次点击就会又立刻显示出来
            AddItemWindow.s_AddItemWindow.Init(rect,
                ItemPathGenerator.GetItemInfo("D:\\GitHub\\test\\Assets", ".txt").pathArray, null, null, null);
            GUIUtility.ExitGUI();
        }
        GUILayout.FlexibleSpace();
        EditorGUILayout.EndHorizontal();
    }
}