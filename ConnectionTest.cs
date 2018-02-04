using JsonFx.U3DEditor;
using ParadoxNotion.Design;
using UnityEditor;
using UnityEngine;

[JsonClassType]
[JsonOptIn]
public abstract class ConnectionTest
{
    protected enum TipConnectionStyle
    {
        None,
        Circle,
        Arrow
    }

    protected const float RELINK_DISTANCE_SNAP = 20f;

    [SerializeField] protected NodeTest m_SourceNode;
    [SerializeField] protected NodeTest m_TargetNode;
    [SerializeField] protected bool m_IsDisabled;
    [SerializeField] protected int m_Id;
    [SerializeField] protected bool m_InfoCollapsed;

    protected Rect m_AreaRect = new Rect(0, 0, 50, 10);

    protected Color m_ConnectionColor = new Color(0.7f, 0.7f, 1f, 0.8f);
    protected float m_LineSize = 3;

    protected Vector3 m_LineFromTangent = Vector3.zero;
    protected Vector3 m_LineToTangent = Vector3.zero;

    protected bool    m_IsRelinking;
    protected Vector3 m_RelinkClickPos;

    protected Rect m_StartPortRect;
    protected Rect m_EndPortRect;

    protected virtual Color defaultColor
    {
        get { return new Color(0.7f, 0.7f, 1f, 0.8f); }
    }

    protected virtual float defaultSize
    {
        get { return 3f; }
    }

    protected virtual TipConnectionStyle tipConnectionStyle
    {
        get { return TipConnectionStyle.Circle; }
    }

    protected virtual bool canRelink
    {
        get { return true; }
    }

    private bool infoExpanded
    {
        get { return !m_InfoCollapsed; }
        set { m_InfoCollapsed = !value; }
    }

    public int id
    {
        get { return m_Id; }
        set { m_Id = value; }
    }

    public NodeTest sourceNode
    {
        get { return m_SourceNode; }
        protected set { m_SourceNode = value; }
    }

    public NodeTest targetNode
    {
        get { return m_TargetNode; }
        protected set { m_TargetNode = value; }
    }

    public bool isActive
    {
        get { return !m_IsDisabled; }
        set
        {
            m_IsDisabled = !value;
            //这样做是不是node不会禁用
            //node
        }
    }

    protected GraphTest graph
    {
        get { return sourceNode.graph; }
    }
 
    public ConnectionTest()
    {

    }

    public static ConnectionTest Create(NodeTest source, NodeTest target, int sourceIndex)
    {
        if (source == null || target == null)
        {
            Debug.LogError("Can't Create a Connection without providing Source and Target Nodes");
            return null;
        }

        var newConnection = (ConnectionTest) System.Activator.CreateInstance(source.outConnectionType);
        Undo.RecordObject(source.graph, "Create Connection");
        newConnection.sourceNode = source;      
        newConnection.targetNode = target;

        source.outConnections.Insert(sourceIndex, newConnection);
        target.inConnections.Add(newConnection);
        newConnection.OnValidate(sourceIndex, target.inConnections.IndexOf(newConnection));
        return newConnection;
    }
    
    public ConnectionTest Duplicate(NodeTest newSource, NodeTest newTarget)
    {
            if (newSource == null || newTarget == null)
            {
                Debug.LogError("Can't Duplicate a Connection without providing NewSource and NewTarget Nodes");
                return null;
            }

            //deep clone
          /*  var newConnection = JSONSerializer.Deserialize<Connection>(JSONSerializer.Serialize(typeof(Connection), this));
            Undo.RecordObject(newSource.graph, "Duplicate Connection");

            newConnection.SetSource(newSource, false);
            newConnection.SetTarget(newTarget, false);

            

            newConnection.OnValidate(newSource.outConnections.IndexOf(newConnection), newTarget.inConnections.IndexOf(newConnection));
            return newConnection;*/
        return null;
    }

    public virtual void OnValidate(int sourceIndex, int targetIndex)
    {

    }

    public virtual void OnDestroy()
    {

    }

    public void SetSource(NodeTest newSource, bool isRelink = true)
    {
        Undo.RecordObject(graph, "Set Source");

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

    public void SetTarget(NodeTest newTarget, bool isRelink = true)
    {
        Undo.RecordObject(graph, "Set Target");

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

    public void DrawConnectionGUI(Vector3 lineFrom, Vector3 lineTo)
    {
        var mlt = 0.8f;
        var tangentX = Mathf.Abs(lineFrom.x - lineTo.x)*mlt;
        var tangentY = Mathf.Abs(lineFrom.y - lineTo.y)*mlt;

        GUI.color = defaultColor;

        m_StartPortRect = new Rect(0, 0, 12, 12);
        m_StartPortRect.center = lineFrom;

        m_EndPortRect = new Rect(0, 0, 15, 15);
        m_EndPortRect.center = lineTo;

        if (lineFrom.x <= sourceNode.nodeRect.x)
        {
            m_LineFromTangent = new Vector3(-tangentX, 0, 0);
        }

        if (lineFrom.x >= sourceNode.nodeRect.xMax)
        {
            m_LineFromTangent = new Vector3(tangentX, 0, 0);
        }

        if (lineFrom.y <= sourceNode.nodeRect.y)
            m_LineFromTangent = new Vector3(0, -tangentY, 0);

        if (lineFrom.y >= sourceNode.nodeRect.yMax)
            m_LineFromTangent = new Vector3(0, tangentY, 0);


        if (lineTo.x <= targetNode.nodeRect.x)
        {
            m_LineToTangent = new Vector3(-tangentX, 0, 0);
            if (tipConnectionStyle == TipConnectionStyle.Circle)
                GUI.Box(m_EndPortRect, string.Empty, "circle");
            else if (tipConnectionStyle == TipConnectionStyle.Arrow)
                GUI.Box(m_EndPortRect, string.Empty, "arrowRight");
        }

        if (lineTo.x >= targetNode.nodeRect.xMax)
        {
            m_LineToTangent = new Vector3(tangentX, 0, 0);
            if (tipConnectionStyle == TipConnectionStyle.Circle)
                GUI.Box(m_EndPortRect, string.Empty, "circle");
            else if (tipConnectionStyle == TipConnectionStyle.Arrow)
                GUI.Box(m_EndPortRect, string.Empty, "arrowLeft");
        }

        if (lineTo.y <= targetNode.nodeRect.y)
        {
            m_LineToTangent = new Vector3(0, -tangentY, 0);
            if (tipConnectionStyle == TipConnectionStyle.Circle)
                GUI.Box(m_EndPortRect, string.Empty, "circle");
            else if (tipConnectionStyle == TipConnectionStyle.Arrow)
                GUI.Box(m_EndPortRect, string.Empty, "arrowBottom");
        }

        if (lineTo.y >= targetNode.nodeRect.yMax)
        {
            m_LineToTangent = new Vector3(0, tangentY, 0);
            if (tipConnectionStyle == TipConnectionStyle.Circle)
                GUI.Box(m_EndPortRect, string.Empty, "circle");
            else if (tipConnectionStyle == TipConnectionStyle.Arrow)
                GUI.Box(m_EndPortRect, string.Empty, "arrowTop");
        }

        GUI.color = Color.white;

        HandleEvents();
        if (!m_IsRelinking || Vector3.Distance(m_RelinkClickPos, Event.current.mousePosition) < RELINK_DISTANCE_SNAP)
        {
            DrawConnection(lineFrom, lineTo);
            DrawInfoRect(lineFrom, lineTo);
        }
    }

    void DrawConnection(Vector3 lineFrom, Vector3 lineTo)
    {
        m_ConnectionColor = isActive ? m_ConnectionColor : new Color(0.3f, 0.3f, 0.3f);
        if (!Application.isPlaying)
        {
            m_ConnectionColor = isActive ? defaultColor : new Color(0.3f, 0.3f, 0.3f);
            var highlight = graph.currentSelection == id || graph.currentSelection == sourceNode.id || graph.currentSelection == targetNode.id;
            m_ConnectionColor.a = highlight ? 1 : m_ConnectionColor.a;
            m_LineSize = highlight ? defaultSize + 2 : defaultSize;
        }

        Handles.color = m_ConnectionColor;
        var shadow = new Vector3(3.5f, 3.5f, 0);
        Handles.DrawBezier(lineFrom, lineTo + shadow, lineFrom + shadow + m_LineFromTangent + shadow, lineTo + shadow + m_LineToTangent, new Color(0, 0, 0, 0.1f), null, m_LineSize + 10f);
        Handles.DrawBezier(lineFrom, lineTo, lineFrom + m_LineFromTangent, lineTo + m_LineToTangent, m_ConnectionColor, null, m_LineSize);
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
        result += 3 * uu * t * (lineFrom + m_LineFromTangent);
        result += 3 * u * tt * (lineTo + m_LineToTangent);
        result += ttt * lineTo;
        var midPosition = (Vector2)result;
        m_AreaRect.center = midPosition;
        var alpha = (infoExpanded || graph.currentSelection == id || graph.currentSelection == sourceNode.id) ? 0.8f : 0.1f;
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

            m_AreaRect.width = finalSize.x;
            m_AreaRect.height = finalSize.y;

            GUI.color = new Color(1f, 1f, 1f, alpha);
            GUI.Box(m_AreaRect, textToShow);
            GUI.color = Color.white;
        }
        else
        {
            m_AreaRect.width = 0;
            m_AreaRect.height = 0;
        }
    }

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
            graph.PostGUI += delegate { graph.RemoveConnection(this); };
            return;
        }

        GUI.color = Color.white;
        GUILayout.EndHorizontal();

        EditorUtils.BoldSeparator();
       // OnConnectionInspectorGUI();
        sourceNode.OnConnectionInspectorGUI(sourceNode.outConnections.IndexOf(this));

        UndoManager.CheckDirty(graph);
    }

    //The information to show in the middle area of the connection
    protected virtual string GetConnectionInfo(bool isExpanded)
    {
        return null;      
    }
 
    void HandleEvents()
    {
        var e = Event.current;
        if ((/*Graph.allowClick &&*/ e.type == EventType.MouseDown && e.button == 0) && (m_AreaRect.Contains(e.mousePosition) || m_StartPortRect.Contains(e.mousePosition) || m_EndPortRect.Contains(e.mousePosition)))
        {
            if (canRelink)
            {
                m_IsRelinking = true;
                m_RelinkClickPos = e.mousePosition;
            }
            graph.currentSelection = id;
            e.Use();
            return;
        }

        if (canRelink && m_IsRelinking)
        {
            if (Vector3.Distance(m_RelinkClickPos, Event.current.mousePosition) > RELINK_DISTANCE_SNAP)
            {
                Handles.DrawBezier(m_StartPortRect.center, e.mousePosition, m_StartPortRect.center, e.mousePosition, defaultColor, null, defaultSize);
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
                    m_IsRelinking = false;
                    e.Use();
                }
            }
            else
            {
                if (e.type == EventType.MouseUp)
                {
                    m_IsRelinking = false;
                }
            }
        }

        if (/*Graph.allowClick &&*/ e.type == EventType.MouseDown && e.button == 1 && m_AreaRect.Contains(e.mousePosition))
        {
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(infoExpanded ? "Collapse Info" : "Expand Info"), false, () => { infoExpanded = !infoExpanded; });
            menu.AddItem(new GUIContent(isActive ? "Disable" : "Enable"), false, () => { isActive = !isActive; });
            menu.AddSeparator("/");
            menu.AddItem(new GUIContent("Delete"), false, () => { graph.RemoveConnection(this); });

            graph.PostGUI += () => { menu.ShowAsContext(); };
            e.Use();
        }
    }
}
 