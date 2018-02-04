using UnityEngine;

public class Bezier : MonoBehaviour
{
    //起始于startPosition，走向于startTangent，并从endTangent来到终点endPosition  
    public Vector3 startPosition = Vector3.zero;
    public Vector3 startTangent = Vector3.zero;
    public Vector3 endPosition = Vector3.zero;
    public Vector3 endTangent = Vector3.zero;
}
