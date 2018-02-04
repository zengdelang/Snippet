using System.Collections;
using System.Collections.Generic;
using JsonFx.U3DEditor;
using ParadoxNotion.Design;
using UnityEditor;
using UnityEngine;

[JsonClassType]
[JsonOptIn]
public abstract class ConnectionTest
{
    [SerializeField] private NodeTest _sourceNode;
    [SerializeField] private NodeTest _targetNode;
    [SerializeField] private bool _isDisabled;

    ///The source node of the connection
    public NodeTest sourceNode
    {
        get { return _sourceNode; }
        protected set { _sourceNode = value; }
    }

    ///The target node of the connection
    public NodeTest targetNode
    {
        get { return _targetNode; }
        protected set { _targetNode = value; }
    }

    ///Is the connection active?
    public bool isActive
    {
        get { return !_isDisabled; }
        set
        {
            if (!_isDisabled && value == false)
            {
                //Reset();
            }
            _isDisabled = !value;
        }
    }

    ///The graph this connection belongs to taken from the source node.
    protected GraphTest graph
    {
        get { return sourceNode.graph; }
    }
 
    public ConnectionTest()
    {

    }
    [SerializeField]
    private int m_Id;

    public int id
    {
        get { return m_Id; }
        set { m_Id = value; }
    }




    ///Create a new Connection. Use this for constructor
    public static ConnectionTest Create(NodeTest source, NodeTest target, int sourceIndex)
    {

        if (source == null || target == null)
        {
            Debug.LogError("Can't Create a Connection without providing Source and Target Nodes");
            return null;
        }


        var newConnection = (ConnectionTest) System.Activator.CreateInstance(source.outConnectionType);

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(source.graph, "Create Connection");
        }
#endif

        newConnection.sourceNode = source;
      
        newConnection.targetNode = target;
        source.outConnections.Insert(sourceIndex, newConnection);
        target.inConnections.Add(newConnection);
        newConnection.OnValidate(sourceIndex, target.inConnections.IndexOf(newConnection));
        return newConnection;
    }

    ///Duplicate the connection providing a new source and target
    public ConnectionTest Duplicate(NodeTest newSource, NodeTest newTarget)
    {
/*
            if (newSource == null || newTarget == null)
            {
                Debug.LogError("Can't Duplicate a Connection without providing NewSource and NewTarget Nodes");
                return null;
            }

            //deep clone
            var newConnection = JSONSerializer.Deserialize<Connection>(JSONSerializer.Serialize(typeof(Connection), this));

#if UNITY_EDITOR
			if (!Application.isPlaying){
				UnityEditor.Undo.RecordObject(newSource.graph, "Duplicate Connection");
			}
#endif

            newConnection.SetSource(newSource, false);
            newConnection.SetTarget(newTarget, false);

            var assignable = this as ITaskAssignable;
            if (assignable != null && assignable.task != null)
            {
                (newConnection as ITaskAssignable).task = assignable.task.Duplicate(newSource.graph);
            }

            newConnection.OnValidate(newSource.outConnections.IndexOf(newConnection), newTarget.inConnections.IndexOf(newConnection));
            return newConnection;*/
        return null;
    }

    ///Called when the Connection is created, duplicated or otherwise needs validation.
    public virtual void OnValidate(int sourceIndex, int targetIndex)
    {
    }

    ///Called when the connection is destroyed (always through graph.RemoveConnection or when a node is removed through graph.RemoveNode)
    public virtual void OnDestroy()
    {
    }

    ///Relinks the source node of the connection
    public void SetSource(NodeTest newSource, bool isRelink = true)
    {

#if UNITY_EDITOR
        if (!Application.isPlaying && graph != null)
        {
            UnityEditor.Undo.RecordObject(graph, "Set Source");
        }
#endif

        if (isRelink)
        {
            var i = sourceNode.outConnections.IndexOf(this);
            sourceNode.OnChildDisconnected(i);
            newSource.OnChildConnected(i);

            sourceNode.outConnections.Remove(this);
        }
        newSource.outConnections.Add(this);

        sourceNode = newSource;
    }

    ///Relinks the target node of the connection
    public void SetTarget(NodeTest newTarget, bool isRelink = true)
    {

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            UnityEditor.Undo.RecordObject(graph, "Set Target");
        }
#endif

        if (isRelink)
        {
            var i = targetNode.inConnections.IndexOf(this);
            targetNode.OnParentDisconnected(i);
            newTarget.OnParentConnected(i);

            targetNode.inConnections.Remove(this);
        }
        newTarget.inConnections.Add(this);

        targetNode = newTarget;
    }


    protected enum TipConnectionStyle
    {
        None,
        Circle,
        Arrow
    }

    [SerializeField]
    private bool _infoCollapsed;

    private const float RELINK_DISTANCE_SNAP = 20f;
    private Rect areaRect = new Rect(0, 0, 50, 10);
   
    private Color connectionColor = new Color(0.7f, 0.7f, 1f, 0.8f); //NodeTest.restingColor;
    private float lineSize = 3;
    private bool nowSwitchingColors = false;
    private Vector3 lineFromTangent = Vector3.zero;
    private Vector3 lineToTangent = Vector3.zero;
    private bool isRelinking = false;
    private Vector3 relinkClickPos;
    private Rect startPortRect;
    private Rect endPortRect;
    private float hor;

    private bool infoExpanded
    {
        get { return !_infoCollapsed; }
        set { _infoCollapsed = !value; }
    }


    virtual protected Color defaultColor
    {
        get { return new Color(0.7f, 0.7f, 1f, 0.8f); } //Node.restingColor; }
    }

    virtual protected float defaultSize
    {
        get { return 3f; }
    }

    virtual protected TipConnectionStyle tipConnectionStyle
    {
        get { return TipConnectionStyle.Circle; }
    }

    virtual protected bool canRelink
    {
        get { return true; }
    }

    //Draw connection from-to
    public void DrawConnectionGUI(Vector3 lineFrom, Vector3 lineTo)
    {

        var mlt = 0f;
        mlt = 0.8f;
        //if (NCPrefs.connectionStyle == NCPrefs.ConnectionStyle.Smooth) { mlt = 0.8f; }
       // if (NCPrefs.connectionStyle == NCPrefs.ConnectionStyle.Stepped) { mlt = 1f; }
        var tangentX = Mathf.Abs(lineFrom.x - lineTo.x) * mlt;
        var tangentY = Mathf.Abs(lineFrom.y - lineTo.y) * mlt;

        GUI.color = defaultColor;

        startPortRect = new Rect(0, 0, 12, 12);
        startPortRect.center = lineFrom;

        endPortRect = new Rect(0, 0, 15, 15);
        endPortRect.center = lineTo;

        hor = 0;

        if (lineFrom.x <= sourceNode.nodeRect.x)
        {
            lineFromTangent = new Vector3(-tangentX, 0, 0);
            hor--;
        }

        if (lineFrom.x >= sourceNode.nodeRect.xMax)
        {
            lineFromTangent = new Vector3(tangentX, 0, 0);
            hor++;
        }

        if (lineFrom.y <= sourceNode.nodeRect.y)
            lineFromTangent = new Vector3(0, -tangentY, 0);

        if (lineFrom.y >= sourceNode.nodeRect.yMax)
            lineFromTangent = new Vector3(0, tangentY, 0);


        if (lineTo.x <= targetNode.nodeRect.x)
        {
            lineToTangent = new Vector3(-tangentX, 0, 0);
            hor--;
            if (tipConnectionStyle == TipConnectionStyle.Circle)
                GUI.Box(endPortRect, string.Empty, (GUIStyle)"circle");
            else
            if (tipConnectionStyle == TipConnectionStyle.Arrow)
                GUI.Box(endPortRect, string.Empty, (GUIStyle)"arrowRight");
        }

        if (lineTo.x >= targetNode.nodeRect.xMax)
        {
            lineToTangent = new Vector3(tangentX, 0, 0);
            hor++;
            if (tipConnectionStyle == TipConnectionStyle.Circle)
                GUI.Box(endPortRect, string.Empty, (GUIStyle)"circle");
            else
            if (tipConnectionStyle == TipConnectionStyle.Arrow)
                GUI.Box(endPortRect, string.Empty, (GUIStyle)"arrowLeft");
        }

        if (lineTo.y <= targetNode.nodeRect.y)
        {
            lineToTangent = new Vector3(0, -tangentY, 0);
            if (tipConnectionStyle == TipConnectionStyle.Circle)
                GUI.Box(endPortRect, string.Empty, (GUIStyle)"circle");
            else
            if (tipConnectionStyle == TipConnectionStyle.Arrow)
                GUI.Box(endPortRect, string.Empty, (GUIStyle)"arrowBottom");
        }

        if (lineTo.y >= targetNode.nodeRect.yMax)
        {
            lineToTangent = new Vector3(0, tangentY, 0);
            if (tipConnectionStyle == TipConnectionStyle.Circle)
                GUI.Box(endPortRect, string.Empty, (GUIStyle)"circle");
            else
            if (tipConnectionStyle == TipConnectionStyle.Arrow)
                GUI.Box(endPortRect, string.Empty, (GUIStyle)"arrowTop");
        }

        GUI.color = Color.white;

       
        HandleEvents();
        if (!isRelinking || Vector3.Distance(relinkClickPos, Event.current.mousePosition) < RELINK_DISTANCE_SNAP)
        {
            DrawConnection(lineFrom, lineTo);
            DrawInfoRect(lineFrom, lineTo);
        }
    }

    //The actual connection graphic
    void DrawConnection(Vector3 lineFrom, Vector3 lineTo)
    {
        connectionColor = isActive ? connectionColor : new Color(0.3f, 0.3f, 0.3f);
        if (!Application.isPlaying)
        {
            connectionColor = isActive ? defaultColor : new Color(0.3f, 0.3f, 0.3f);
            var highlight = /*graph.currentSelection == this.Id ||*/ graph.currentSelection == sourceNode.id || graph.currentSelection == targetNode.id;
            connectionColor.a = highlight ? 1 : connectionColor.a;
            lineSize = highlight ? defaultSize + 2 : defaultSize;
        }

        Handles.color = connectionColor;
        /*if (NCPrefs.connectionStyle == NCPrefs.ConnectionStyle.Smooth)
        {
            var shadow = new Vector3(3.5f, 3.5f, 0);
            Handles.DrawBezier(lineFrom, lineTo + shadow, lineFrom + shadow + lineFromTangent + shadow, lineTo + shadow + lineToTangent, new Color(0, 0, 0, 0.1f), null, lineSize + 10f);
            Handles.DrawBezier(lineFrom, lineTo, lineFrom + lineFromTangent, lineTo + lineToTangent, connectionColor, null, lineSize);
        }
        else if (NCPrefs.connectionStyle == NCPrefs.ConnectionStyle.Stepped)
        {
            var shadow = new Vector3(1, 1, 0);
            Handles.DrawPolyLine(lineFrom, lineFrom + lineFromTangent * (hor == 0 ? 0.5f : 1), lineTo + lineToTangent * (hor == 0 ? 0.5f : 1), lineTo);
            Handles.DrawPolyLine(lineFrom + shadow, (lineFrom + lineFromTangent * (hor == 0 ? 0.5f : 1)) + shadow, (lineTo + lineToTangent * (hor == 0 ? 0.5f : 1)) + shadow, lineTo + shadow);
        }
        else*/
        /*{
            Handles.DrawBezier(lineFrom, lineTo, lineFrom, lineTo, connectionColor, null, lineSize);
        }*/
        var shadow = new Vector3(3.5f, 3.5f, 0);
        Handles.DrawBezier(lineFrom, lineTo + shadow, lineFrom + shadow + lineFromTangent + shadow, lineTo + shadow + lineToTangent, new Color(0, 0, 0, 0.1f), null, lineSize + 10f);
        Handles.DrawBezier(lineFrom, lineTo, lineFrom + lineFromTangent, lineTo + lineToTangent, connectionColor, null, lineSize);
        Handles.color = Color.white;
    }

    //Information showing in the middle
    void DrawInfoRect(Vector3 lineFrom, Vector3 lineTo)
    {
        var t = 0.5f;
        float u = 1.0f - t;
        float tt = t * t;
        float uu = u * u;
        float uuu = uu * u;
        float ttt = tt * t;
        Vector3 result = uuu * lineFrom;
        result += 3 * uu * t * (lineFrom + lineFromTangent);
        result += 3 * u * tt * (lineTo + lineToTangent);
        result += ttt * lineTo;
        var midPosition = (Vector2)result;
        areaRect.center = midPosition;
        //ConnectionTest没有id,选中是一个大问题
        var alpha = (infoExpanded ||/* Graph.currentSelection == this ||*/ graph.currentSelection == sourceNode.id) ? 0.8f : 0.1f;
        var info = GetConnectionInfo(infoExpanded);
        var extraInfo = sourceNode.GetConnectionInfo(sourceNode.outConnections.IndexOf(this));
        if (!string.IsNullOrEmpty(info) || !string.IsNullOrEmpty(extraInfo))
        {

            if (!string.IsNullOrEmpty(extraInfo) && !string.IsNullOrEmpty(info))
            {
                extraInfo = "\n" + extraInfo;
            }

            var textToShow = string.Format("<size=9>{0}{1}</size>", info, extraInfo);
            if (!infoExpanded)
            {
                textToShow = "<size=9>-||-</size>";
            }
            var finalSize = GUI.skin.GetStyle("Box").CalcSize(new GUIContent(textToShow));

            areaRect.width = finalSize.x;
            areaRect.height = finalSize.y;

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Box(areaRect, textToShow);
            GUI.color = Color.white;

        }
        else
        {

            areaRect.width = 0;
            areaRect.height = 0;
        }
    }


    //The connection's inspector
    public void ShowConnectionInspectorGUI()
    {

        UndoManager.CheckUndo(graph, "Connection Inspector");

        GUILayout.BeginHorizontal();
        GUI.color = new Color(1, 1, 1, 0.5f);

        if (GUILayout.Button("◄", GUILayout.Height(14), GUILayout.Width(20)))
        {
            graph.currentSelection = sourceNode.id;
        }

        if (GUILayout.Button("►", GUILayout.Height(14), GUILayout.Width(20)))
        {
            graph.currentSelection = targetNode.id;
        }

        isActive = EditorGUILayout.ToggleLeft("ACTIVE", isActive, GUILayout.Width(150));

        GUILayout.FlexibleSpace();

        if (GUILayout.Button("X", GUILayout.Height(14), GUILayout.Width(20)))
        {
            //graph.PostGUI += delegate { graph.RemoveConnection(this); };
            return;
        }

        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        EditorUtils.BoldSeparator();
        OnConnectionInspectorGUI();
        sourceNode.OnConnectionInspectorGUI(sourceNode.outConnections.IndexOf(this));

        UndoManager.CheckDirty(graph);
    }

    //The information to show in the middle area of the connection
    virtual protected string GetConnectionInfo(bool isExpanded) { return null; }
    //Editor.Override to show controls in the editor panel when connection is selected
    virtual protected void OnConnectionInspectorGUI() { }


    void HandleEvents()
    {

        var e = Event.current;
        //On click select this connection
        if ((/*Graph.allowClick &&*/ e.type == EventType.MouseDown && e.button == 0) && (areaRect.Contains(e.mousePosition) || startPortRect.Contains(e.mousePosition) || endPortRect.Contains(e.mousePosition)))
        {
            if (canRelink)
            {
                isRelinking = true;
                relinkClickPos = e.mousePosition;
            }
           // Graph.currentSelection = this;
            e.Use();
            return;
        }

        if (canRelink && isRelinking)
        {
            if (Vector3.Distance(relinkClickPos, Event.current.mousePosition) > RELINK_DISTANCE_SNAP)
            {
                Handles.DrawBezier(startPortRect.center, e.mousePosition, startPortRect.center, e.mousePosition, defaultColor, null, defaultSize);
                if (e.type == EventType.MouseUp)
                {
                    foreach (var node in graph.allNodes)
                    {
                        if (node != targetNode && node != sourceNode && node.nodeRect.Contains(e.mousePosition) && node.IsNewConnectionAllowed())
                        {
                            SetTarget(node);
                            break;
                        }
                    }
                    isRelinking = false;
                    e.Use();
                }
            }
            else
            {
                if (e.type == EventType.MouseUp)
                {
                    isRelinking = false;
                }
            }
        }

        if (/*Graph.allowClick &&*/ e.type == EventType.MouseDown && e.button == 1 && areaRect.Contains(e.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(infoExpanded ? "Collapse Info" : "Expand Info"), false, () => { infoExpanded = !infoExpanded; });
            menu.AddItem(new GUIContent(isActive ? "Disable" : "Enable"), false, () => { isActive = !isActive; });

            

            menu.AddSeparator("/");
//            menu.AddItem(new GUIContent("Delete"), false, () => { graph.RemoveConnection(this); });

            graph.PostGUI += () => { menu.ShowAsContext(); };
            e.Use();
        }
    }
}
 