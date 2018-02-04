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


}