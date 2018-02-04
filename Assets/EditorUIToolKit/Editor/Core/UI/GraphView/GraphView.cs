using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Graphs;
using UnityEngine;

public class GraphView : View
{
    public GraphTest currentGraph;

    protected Rect clientAreaRect;
    protected Rect viewRect;
    protected GUISkin guiSkin;
    protected bool fullDrawPass = true;
 
    private Vector2? smoothPan;
    private float? smoothZoomFactor;
    private Vector2 _panVelocity = Vector2.one;
    private float _zoomVelocity = 1;

    private readonly static float unityTabHeight = 22;
    private readonly static int gridSize = 15;
    private readonly static Vector2 virtualCenterOffset = new Vector2(-5000, -5000);
 
    private Vector2 pan 
    {
        get { return currentGraph != null ? Vector2.Min(currentGraph.translation, Vector2.zero) : virtualCenter; }
        set
        {
            if (currentGraph != null)
            {
                var t = currentGraph.translation;
                t = Vector2.Min(value, Vector2.zero);
                if (smoothPan == null)
                {
                    t.x = Mathf.Round(t.x); //pixel perfect correction
                    t.y = Mathf.Round(t.y); //pixel perfect correction
                }
                currentGraph.translation = t;
            }
        }
    }

    private float zoomFactor
    {
        get { return currentGraph != null ? Mathf.Clamp(currentGraph.zoomFactor, 0.25f, 1f) : 1f; }
        set
        {
            if (currentGraph != null) currentGraph.zoomFactor = Mathf.Clamp(value, 0.25f, 1f);
        }
    }
 
    private Vector2 virtualCenter
    {
        get { return -virtualCenterOffset + viewRect.size / 2; }
    }

    private Vector2 mousePosInCanvas
    {
        get { return ViewSpaceToCanvasSpace(Event.current.mousePosition); }
    }
 
    public GraphView(ViewGroupManager owner) : base(owner)
    {
        guiSkin = (GUISkin) Resources.Load(EditorGUIUtility.isProSkin ? "NodeCanvasSkin" : "NodeCanvasSkinLight");
    }

    Vector2 ViewSpaceToCanvasSpace(Vector2 viewPos)
    {
        viewPos -= new Vector2(clientAreaRect.x, clientAreaRect.y);
        return (viewPos - pan) / zoomFactor;
    }

    Vector2 CanvasSpaceToViewSpace(Vector2 canvasPos)
    {
        return (canvasPos * zoomFactor) + pan + new Vector2(clientAreaRect.x, clientAreaRect.y); ;
    }

    public override void Update()
    {
        DoSmoothPan();
        DoSmoothZoom();
    }

    void DoSmoothPan()
    {
        if (smoothPan == null)
        {
            return;
        }

        var targetPan = (Vector2)smoothPan;
        if ((targetPan - pan).magnitude < 0.1f)
        {
            smoothPan = null;
            return;
        }

        pan = Vector2.SmoothDamp(pan, targetPan, ref _panVelocity, 0.05f, Mathf.Infinity,
            Application.isPlaying ? Time.deltaTime : 1f / 200);
        Repaint();
    }

    void DoSmoothZoom()
    {
        if (smoothZoomFactor == null)
        {
            return;
        }

        var targetZoom = (float)smoothZoomFactor;
        if (Mathf.Abs(targetZoom - zoomFactor) < 0.00001f)
        {
            smoothZoomFactor = null;
            return;
        }

        zoomFactor = Mathf.SmoothDamp(zoomFactor, targetZoom, ref _zoomVelocity, 0.05f, Mathf.Infinity, Application.isPlaying ? Time.deltaTime : 1f / 200);
        if (zoomFactor > 0.99999f)
        {
            zoomFactor = 1;
        }
        Repaint();
    }

    public override void OnInspectorUpdate()
    {
        base.OnInspectorUpdate(); 
        Repaint();
    }

    public override void OnGUI(Rect rect)
    {
        base.OnGUI(rect);
        //编译时候序列化
        var keyboardControlID = GUIUtility.GetControlID(FocusType.Keyboard);

        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
        GUI.skin.label.richText = true;
        GUI.skin = guiSkin;

        HandleEvents(keyboardControlID);

        rect.y += 22;
        rect.height -= 22;
        clientAreaRect = new Rect(100, 100, rect.width - 500, rect.height - 300);
        clientAreaRect.y += unityTabHeight;
        Matrix4x4 oldMatrix;
 
        clientAreaRect = StartZoomArea(clientAreaRect, out oldMatrix);
        GUI.BeginGroup(clientAreaRect); 

            var totalCanvas = clientAreaRect;
            totalCanvas.x = pan.x/zoomFactor;
            totalCanvas.y = pan.y/zoomFactor;
            //totalCanvas.width = canvasRect.width  已经/zoomFactor
            //实际根据pan来增加宽度，向右移动，pan.x是负的
            totalCanvas.width -= pan.x / zoomFactor;
            totalCanvas.height -= pan.y / zoomFactor;

            GUI.BeginGroup(totalCanvas);

                viewRect = totalCanvas;
                viewRect.x = -pan.x / zoomFactor;
                viewRect.y = -pan.y / zoomFactor;
                viewRect.width += pan.x / zoomFactor;
                viewRect.height += pan.y / zoomFactor;
                DrawGrid(viewRect,pan, zoomFactor);

                if (currentGraph != null)
                {
                    currentGraph.keyboardControl = keyboardControlID;
                    Owner.WindowOwner.BeginWindows();
                    currentGraph.ShowNodesGUI(Event.current, viewRect, fullDrawPass, mousePosInCanvas, zoomFactor);
                    Owner.WindowOwner.EndWindows();
                }

                DoCanvasRectSelection(viewRect, Event.current);
        GUI.EndGroup();

        GUI.EndGroup();
        EndZoomArea(oldMatrix);

        //ShowScrollBars();
 
        if (currentGraph != null) 
            currentGraph.ShowGraphControls(Event.current, mousePosInCanvas);

        GUI.skin = null;
        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
    }

    void DrawGrid(Rect container, Vector2 offset, float zoomFactor)
    {
        var scaledX = (container.width - offset.x) / zoomFactor;
        var scaledY = (container.height - offset.y) / zoomFactor;

        for (var i = 0 - (int)offset.x; i < scaledX; i++)
        {
            if (i % gridSize == 0)
            {
                Handles.color = new Color(0, 0, 0, i % (gridSize * 5) == 0 ? 0.2f : 0.1f);
                Handles.DrawLine(new Vector3(i, 0, 0), new Vector3(i, scaledY, 0));
            }
        }

        for (var i = 0 - (int)offset.y; i < scaledY; i++)
        {
            if (i % gridSize == 0)
            {
                Handles.color = new Color(0, 0, 0, i % (gridSize * 5) == 0 ? 0.2f : 0.1f);
                Handles.DrawLine(new Vector3(0, i, 0), new Vector3(scaledX, i, 0));
            }
        }

        Handles.color = Color.white;
    }

    Rect StartZoomArea(Rect container, out Matrix4x4 oldMatrix)
    {
        GUI.EndGroup();

        GUI.Box(clientAreaRect, "", "canvasBG");
       
        container.width /= zoomFactor;
        container.height /= zoomFactor;

        oldMatrix = GUI.matrix;
        var matrix1 = Matrix4x4.TRS(new Vector3(container.x, container.y), Quaternion.identity, Vector3.one);
        var matrix2 = Matrix4x4.Scale(new Vector3(zoomFactor, zoomFactor, zoomFactor));
        GUI.matrix = matrix1 * matrix2 * matrix1.inverse * GUI.matrix;
        return container;
    }

    void EndZoomArea(Matrix4x4 oldMatrix)
    {
        GUI.matrix = oldMatrix;
        var zoomRecoveryRect = new Rect(0, 0, EditorGUIUtility.currentViewWidth, Screen.height);
        GUI.BeginGroup(zoomRecoveryRect, GUIStyle.none);
    }

    void HandleEvents(int keyboardControlID)
    {
        Event e = Event.current;
        var rect = clientAreaRect;
        rect.y -= unityTabHeight;
        if (!rect.Contains(e.mousePosition))
        {

            return;
        }
     
        if (Event.current.type == EventType.MouseDown)
        {
            GUIUtility.keyboardControl = keyboardControlID;
        }

        if (e.type == EventType.KeyDown && e.keyCode == KeyCode.F && GUIUtility.keyboardControl == keyboardControlID)
        {
            if (currentGraph.allNodes.Count > 0)
            {
                FocusPosition(GetNodeBounds(currentGraph.allNodes, viewRect).center);
            }
            else
                FocusPosition(virtualCenter);
        }

        if (e.type == EventType.MouseDown && e.button == 2 && e.clickCount == 2)
        {
            FocusPosition(ViewSpaceToCanvasSpace(e.mousePosition));
        }

        if (e.type == EventType.ScrollWheel)
        {
            var zoomDelta = e.shift ? 0.1f : 0.25f;
            ZoomAt(e.mousePosition, -e.delta.y > 0 ? zoomDelta : -zoomDelta);
        }

        if ((e.button == 2 && e.type == EventType.MouseDrag)
            || ((e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.alt && e.isMouse))
        {
            pan += e.delta;
            smoothPan = null;
            smoothZoomFactor = null;
            e.Use();
        }
    } 

    private bool isMultiSelecting;
    private Vector2 selectionStartPos;

    void DoCanvasRectSelection(Rect container, Event e)
    {
        if (/*Graph.allowClick && */ e.type == EventType.MouseDown && e.button == 0 && !e.alt && !e.shift && clientAreaRect.Contains(CanvasSpaceToViewSpace(e.mousePosition)))
        {
            currentGraph.currentSelection = -1;
            selectionStartPos = e.mousePosition;
            isMultiSelecting = true;
            e.Use();
        }

        if (isMultiSelecting && e.rawType == EventType.MouseUp)
        {
            var rect = GetSelectionRect(selectionStartPos, e.mousePosition);
            var overlapedNodes = currentGraph.allNodes.Where(n => rect.Overlaps(n.nodeRect) && !n.isHidden).ToList();
            isMultiSelecting = false;

            if (e.control && rect.width > 50 && rect.height > 50)
            {
                /*Undo.RegisterCompleteObjectUndo(currentGraph, "Create Group");
                if (currentGraph.canvasGroups == null)
                {
                    currentGraph.canvasGroups = new List<CanvasGroup>();
                }
                currentGraph.canvasGroups.Add(new CanvasGroup(rect, "New Canvas Group"));*/
            }
            else
            {
                if (overlapedNodes.Count > 0)
                {
                    List<int> idList = new List<int>();
                    foreach (var item in overlapedNodes.Cast<object>().ToList())
                    {
                        var node = item as NodeTest;
                        if(node != null)
                            idList.Add(node.id);
                    }
                    currentGraph.multiSelection = idList;
                    e.Use();
                }
            }
        }

        if (isMultiSelecting)
        {
            var rect = GetSelectionRect(selectionStartPos, e.mousePosition);
            if (rect.width > 5 && rect.height > 5)
            {
                GUI.color = new Color(0.5f, 0.5f, 1, 0.3f);
                GUI.Box(rect, string.Empty);
                foreach (var node in currentGraph.allNodes)
                {
                    if (rect.Overlaps(node.nodeRect) && !node.isHidden)
                    {
                        var highlightRect = node.nodeRect;
                        GUI.Box(highlightRect, string.Empty, "windowHighlight");
                    }
                }
                if (rect.width > 50 && rect.height > 50)
                {
                    GUI.color = new Color(1, 1, 1, e.control ? 0.6f : 0.15f);
                    GUI.Label(new Rect(e.mousePosition.x + 16, e.mousePosition.y, 120, 22), "<i>+ control for group</i>");
                }
            }
        }

        GUI.color = Color.white;
    }

    //Get a rect from-to for selection
    Rect GetSelectionRect(Vector2 startPos, Vector2 endPos)
    {
        var num1 = (startPos.x < endPos.x) ? startPos.x : endPos.x;
        var num2 = (startPos.x > endPos.x) ? startPos.x : endPos.x;
        var num3 = (startPos.y < endPos.y) ? startPos.y : endPos.y;
        var num4 = (startPos.y > endPos.y) ? startPos.y : endPos.y;
        return new Rect(num1, num3, num2 - num1, num4 - num3);
    }

    void FocusPosition(Vector2 targetPos) 
    {
        smoothPan = -targetPos;
        smoothPan += new Vector2(viewRect.width / 2, viewRect.height / 2);
        smoothPan *= zoomFactor;
    }

    void ZoomAt(Vector2 center, float delta)
    {
        var pinPoint = (center - pan) / zoomFactor;
        var newZ = zoomFactor;
        newZ += delta;
        newZ = Mathf.Clamp(newZ, 0.25f, 1f);
        smoothZoomFactor = newZ;
        var a = (pinPoint * newZ) + pan;
        var b = center;
        var diff = b - a;
        smoothPan = pan + diff;
    }

    Rect GetNodeBounds(List<NodeTest> nodes, Rect container, bool expandToContainer = false)
    {
        if (nodes == null)
        {
            return container;
        }

        var minX = float.PositiveInfinity;
        var minY = float.PositiveInfinity;
        var maxX = float.NegativeInfinity;
        var maxY = float.NegativeInfinity;

        for (var i = 0; i < nodes.Count; i++)
        {
            if (nodes[i] != null)
            {
                minX = Mathf.Min(minX, nodes[i].nodeRect.xMin);
                minY = Mathf.Min(minY, nodes[i].nodeRect.yMin);
                maxX = Mathf.Max(maxX, nodes[i].nodeRect.xMax);
                maxY = Mathf.Max(maxY, nodes[i].nodeRect.yMax);
            }
        }

        minX -= 20;
        minY -= 20;
        maxX += 20;
        maxY += 20;

        if (expandToContainer)
        {
            minX = Mathf.Min(minX, container.xMin + 20);
            minY = Mathf.Min(minY, container.yMin + 20);
            maxX = Mathf.Max(maxX, container.xMax - 20);
            maxY = Mathf.Max(maxY, container.yMax - 20);
        }

        return Rect.MinMaxRect(minX, minY, maxX, maxY);
    }
}