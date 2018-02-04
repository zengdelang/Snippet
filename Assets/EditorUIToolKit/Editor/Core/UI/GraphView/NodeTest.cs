using System.Collections.Generic;
using System.Linq;
using JsonFx.U3DEditor;
using NodeCanvas.Editor;
using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEditor;
using UnityEngine;

[JsonClassType]
[JsonOptIn]
public abstract class NodeTest
{
    [SerializeField] private Vector2 m_Position = Vector2.zero;
    [SerializeField] private string  m_CustomName;
    [SerializeField] private string  m_Tag;
    [SerializeField] private string  m_Comment;

    private GraphTest m_Graph;
    [SerializeField]
    private int m_Id;

    [SerializeField]
    protected List<ConnectionTest> _inConnections = new List<ConnectionTest>();

    [SerializeField]
    protected List<ConnectionTest> _outConnections = new List<ConnectionTest>();


    public List<ConnectionTest> inConnections
    {
        get { return _inConnections; }
        protected set { _inConnections = value; }
    }

    public List<ConnectionTest> outConnections
    {
        get { return _outConnections; }
        protected set { _outConnections = value; }
    }

    public Vector2 nodePosition
    {
        get { return m_Position; }
        set { m_Position = value; }
    }

    public GraphTest graph
    {
        get { return m_Graph; }
        set { m_Graph = value; }
    }

    public int id
    {
        get { return m_Id; }
        set { m_Id = value; }
    }

    //The custom title name of the node if any
    private string customName
    {
        get { return m_CustomName; }
        set { m_CustomName = value; }
    }

    ///The node tag. Useful for finding nodes through code
    public string tag
    {
        get { return m_Tag; }
        set { m_Tag = value; }
    }

    ///The comments of the node if any
    public string nodeComment
    {
        get { return m_Comment; }
        set { m_Comment = value; }
    }

    public virtual bool showCommentsBottom
    {
        get { return true; }
    }

    ///The numer of possible inputs. -1 for infinite
    abstract public int maxInConnections { get; }


    public abstract System.Type outConnectionType { get; }

    public static NodeTest Create(GraphTest targetGraph, System.Type nodeType, Vector2 pos)
    {
        if (targetGraph == null)
        {
            Debug.LogError("Can not Create a Node without providing a Target Graph");
            return null;
        }

        var newNode = (NodeTest) System.Activator.CreateInstance(nodeType);
        Undo.RecordObject(targetGraph, "Create Node");

        newNode.graph = targetGraph;
        newNode.nodePosition = pos;
        return newNode;
    }

    //Class for the nodeports GUI
    class GUIPort
    {
        public readonly int portIndex;
        public readonly NodeTest parent;
        public readonly Vector2 pos;

        public GUIPort(int index, NodeTest parent, Vector2 pos)
        {
            this.portIndex = index;
            this.parent = parent;
            this.pos = pos;
        }
    }

    [SerializeField] private Color _nodeColor;

    private Texture2D _icon;
    private Vector2 size = new Vector2(100, 20);
    private string hexColor { get; set; }
    private bool iconLoaded { get; set; }
    private bool colorLoaded { get; set; }
    private bool hasColorAttribute { get; set; }
    private bool nodeIsPressed { get; set; }

    private const string DEFAULT_HEX_COLOR = "eed9a7";
    private static GUIPort clickedPort { get; set; }
    private static GUIStyle _centerLabel = null;
    private static int dragDropMisses;

    // public static Node[] copiedNodes { get; set; }
 
    protected Color restingColor = new Color(0.7f, 0.7f, 1f, 0.8f);
    protected Vector2 minSize = new Vector2(100, 20);

    [System.NonSerialized]
    private string m_NodeName;

    virtual public string name
    {
        get
        {
            if (!string.IsNullOrEmpty(customName))
            {
                return customName;
            }

            if (string.IsNullOrEmpty(m_NodeName))
            {
                var nameAtt = this.GetType().RTGetAttribute<NameAttribute>(false);
                m_NodeName = nameAtt != null ? nameAtt.name : GetType().FriendlyName().SplitCamelCase();
            }
            return m_NodeName;
        }
        set { customName = value; }
    }

    //This is to be able to work with rects which is easier in many cases.
    //Size is temporary to the node since it's auto adjusted thus no need to serialize it
    public Rect nodeRect
    {
        get { return new Rect(m_Position.x, m_Position.y, size.x, size.y); }
        set
        {
            m_Position = new Vector2(value.x, value.y);
            size = new Vector2(Mathf.Max(value.width, minSize.x), Mathf.Max(value.height, minSize.y));
        }
    }

    public bool isActive
    {
        get
        {
            for (var i = 0; i < inConnections.Count; i++)
            {
                if (inConnections[i].isActive)
                {
                    return true;
                }
            }
            return inConnections.Count == 0;
        }
    }

    public bool isSelected
    {
        get { return graph.currentSelection == id || graph.multiSelection.Contains(id); }
    }

    //Label style with alignment in center
    private static GUIStyle centerLabel
    {
        get
        {
            if (_centerLabel == null)
            {
                _centerLabel = new GUIStyle("label");
                _centerLabel.alignment = TextAnchor.UpperCenter;
                _centerLabel.richText = true;
            }
            return _centerLabel;
        }
    }

    //The coloring of the node if any. Default is Color.clear (no coloring).
    private Color nodeColor
    {
        get
        {
            if (colorLoaded)
            {
                return _nodeColor;
            }

            ResolveColoring(_nodeColor);
            colorLoaded = true;
            return _nodeColor;
        }
        set
        {
            if (_nodeColor != value)
            {
                _nodeColor = value;
                ResolveColoring(value);
            }
        }
    }

    abstract public int maxOutConnections { get; }

    void ResolveColoring(Color color)
    {
        hasColorAttribute = false;
        var cAtt = this.GetType().RTGetAttribute<ColorAttribute>(true);
        if (cAtt != null && cAtt.hexColor.Length == 6)
        {
            hasColorAttribute = true;
            var r = byte.Parse(cAtt.hexColor.Substring(0, 2), System.Globalization.NumberStyles.HexNumber);
            var g = byte.Parse(cAtt.hexColor.Substring(2, 2), System.Globalization.NumberStyles.HexNumber);
            var b = byte.Parse(cAtt.hexColor.Substring(4, 2), System.Globalization.NumberStyles.HexNumber);
            _nodeColor = new Color32(r, g, b, 255);
            hexColor = cAtt.hexColor;
            return;
        }

        if (color.a <= 0.2f)
        {
            _nodeColor = Color.clear;
            hexColor = DEFAULT_HEX_COLOR;
            return;
        }

        _nodeColor = color;
        var temp = (Color32) color;
        hexColor = (temp.r.ToString("X2") + temp.g.ToString("X2") + temp.b.ToString("X2")).ToLower();
    }

    //Helper function to create a nested graph for an IGraphAssignable
    protected static void CreateNested<T>(IGraphAssignable parent) where T : Graph
    {
        var newGraph = EditorUtils.CreateAsset<T>(true);
        if (newGraph != null)
        {
            Undo.RegisterCreatedObjectUndo(newGraph, "CreateNested");
            parent.nestedGraph = newGraph;
        }
    }

    ///Get connection information node wise, to show on top of the connection
    public virtual string GetConnectionInfo(int index)
    {
        return null;
    }

    ///Extra inspector controls for the provided OUT connection
    public virtual void OnConnectionInspectorGUI(int index)
    {

    }

    //The main function for drawing a node's gui.Fires off others.
    public virtual void ShowNodeGUI(Rect drawCanvas, bool fullDrawPass, Vector2 canvasMousePos, float zoomFactor)
    {
        if (fullDrawPass || drawCanvas.Overlaps(nodeRect))
        {
            DrawNodeWindow(canvasMousePos, zoomFactor);
            DrawNodeTag();
            DrawNodeComments(); 
            DrawNodeID();
        }

        DrawNodeConnections(drawCanvas, fullDrawPass, canvasMousePos, zoomFactor);
    }

    protected virtual void DrawNodeID()
    {
        var rect = new Rect(nodeRect.x, nodeRect.y - 20, nodeRect.width, 20);
        GUI.Label(rect, id + " | " + (graph.allNodes.IndexOf(this) + 1));
    }

    public virtual void OnDestroy() { }

    //Draw the window
    protected virtual void DrawNodeWindow(Vector2 canvasMousePos, float zoomFactor)
    {  
        GUI.color = isActive ? Color.white : new Color(0.9f, 0.9f, 0.9f, 0.8f);
        GUI.color = Graph.currentSelection == this ? new Color(0.9f, 0.9f, 1) : GUI.color;
        nodeRect = GUILayout.Window(id, nodeRect, NodeWindowGUI, string.Empty, "window");

        GUI.Box(nodeRect, string.Empty, "windowShadow");
        GUI.color = new Color(1, 1, 1, 0.5f);
        GUI.Box(new Rect(nodeRect.x + 6, nodeRect.y + 6, nodeRect.width, nodeRect.height), string.Empty, "windowShadow");

       if (isSelected)
        {
            GUI.color = restingColor;
            GUI.Box(nodeRect, string.Empty, "windowHighlight");
        }


        GUI.color = Color.white;
     //   if (Graph.allowClick)
        {
            EditorGUIUtility.AddCursorRect(
                new Rect(nodeRect.x * zoomFactor, nodeRect.y * zoomFactor, nodeRect.width * zoomFactor,
                    nodeRect.height * zoomFactor), MouseCursor.Link);
        }
    }


    //This is the callback function of the GUILayout.window. Everything here is called INSIDE the node Window callback.
    //The Window ID is the same as this node's ID.
    void NodeWindowGUI(int ID)
    {
        var e = Event.current;
        ShowHeader();
        HandleEvents(e);
        ShowNodeContents();
        HandleContextMenu(e);
        HandleNodePosition(e);
    }

    //The title name or icon of the node
    void ShowHeader()
    {
        if (name != null)
        {
            if (!EditorGUIUtility.isProSkin)
            {
                //fix light coloring by adding a dark background
                GUI.color = new Color(1, 1, 1, 0.75f);
                GUI.Box(new Rect(0, 3, nodeRect.width, 23), string.Empty);
                GUI.color = Color.white;
            }

            GUILayout.Label(string.Format("<b><size=12><color=#{0}>{1}</color></size></b>", hexColor, name),
                centerLabel);
        }


        if (name != null && nodeColor.a > 0.2f && (!hasColorAttribute))
        {
            var lastRect = GUILayoutUtility.GetLastRect();
            var hMargin = EditorGUIUtility.isProSkin ? 4 : 1;
            GUILayout.Space(2);
            GUI.color = nodeColor;
            GUI.DrawTexture(new Rect(hMargin, lastRect.yMax, nodeRect.width - (hMargin * 2), 3),
                EditorGUIUtility.whiteTexture);
            GUI.color = Color.white;
        }
    }

    //Handles events, Mouse downs, ups etc.
    void HandleEvents(Event e)
    {
        //Node click
        if (e.type == EventType.MouseDown /*&& Graph.allowClick*/ && e.button != 2)
        {
            Undo.RegisterCompleteObjectUndo(graph, "Move Node");

            if (!e.control)
            {
                graph.currentSelection = id;
            }

            if (e.control)
            {
                if (isSelected)
                {
                    graph.multiSelection.Remove(id);
                }
                else
                {
                    graph.multiSelection.Add(id);
                }
            }

            if (e.button == 0)
            {
                nodeIsPressed = true;
            }

            //Double click
            if (e.button == 0 && e.clickCount == 2)
            {
                EditorUtils.OpenScriptOfType(GetType());
                e.Use();
            }

            OnNodePicked();
        }

        if (e.type == EventType.MouseUp)
        {
            nodeIsPressed = false;
            OnNodeReleased();
        }
    }

    //Shows the actual node contents GUI
    void ShowNodeContents()
    {
        GUI.color = Color.white;
        GUI.skin = null;
        GUI.skin.label.richText = true;
        GUI.skin.label.alignment = TextAnchor.MiddleCenter;

        OnNodeGUI();
        GUI.skin.label.alignment = TextAnchor.UpperLeft;
    }

    //Handles and shows the right click mouse button for the node context menu
    void HandleContextMenu(Event e)
    {
        var isContextClick = (e.type == EventType.MouseUp && e.button == 1) ||
                             (e.control && e.type == EventType.MouseUp || e.type == EventType.ContextClick);
        if (/*graph.allowClick &&*/ isContextClick)
        {
            //Multiselection menu
            if (graph.multiSelection.Count > 1)
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Duplicate Selected Nodes"), false, () =>
                {
                //    var newNodes = Graph.CopyNodesToGraph(Graph.multiSelection.OfType<Node>().ToList(), graph);
                 //   Graph.multiSelection = newNodes.Cast<object>().ToList();
                });
                ///    menu.AddItem(new GUIContent("Copy Selected Nodes"), false, () => { copiedNodes = Graph.multiSelection.OfType<Node>().ToArray(); });
                menu.AddSeparator("/");
                menu.AddItem(new GUIContent("Delete Selected Nodes"), false, () =>
                {
                    foreach (var nodeId in graph.multiSelection.ToArray())
                        graph.RemoveNode(nodeId);
                });
                graph.PostGUI += () => { menu.ShowAsContext(); }; 
                e.Use();
            }
            else                 //Single node menu
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("Duplicate (CTRL+D)"), false, () => { });//graph.currentSelection = Duplicate(graph); });
                //menu.AddItem(new GUIContent("Copy Node"), false, () => { copiedNodes = new Node[] { this }; });

                if (inConnections.Count > 0)
                    menu.AddItem(new GUIContent(isActive ? "Disable" : "Enable"), false, () => { SetActive(!isActive); });

                menu = OnContextMenu(menu);
                if (menu != null)
                {
                    menu.AddSeparator("/");
                    menu.AddItem(new GUIContent("Delete (DEL)"), false, () => { graph.RemoveNode(this); });
                    graph.PostGUI += () => { menu.ShowAsContext(); };
                }
                e.Use();
            }
        }
    }

    //basicaly handles the node position and draging etc
    void HandleNodePosition(Event e)
    {
        if (/*Graph.allowClick && */e.button != 2)
        {
            //drag all selected nodes
            if (e.type == EventType.MouseDrag && graph.multiSelection.Count > 1)
            {
              /*  for (var i = 0; i < graph.multiSelection.Count; i++)
                {
                    ((Node)graph.multiSelection[i]).nodePosition += e.delta;
                }
                return;*/
            }
            GUI.DragWindow();
        }
    }

    //The comments of the node sitting next or bottom of it
    void DrawNodeComments()
    {
         if (!string.IsNullOrEmpty(nodeComment))
         {
             var commentsRect = new Rect();
             var size = new GUIStyle("textArea").CalcSize(new GUIContent(nodeComment));

             if (showCommentsBottom)
             {
                 size.y = new GUIStyle("textArea").CalcHeight(new GUIContent(nodeComment), nodeRect.width);
                 commentsRect = new Rect(nodeRect.x, nodeRect.yMax + 5, nodeRect.width, size.y);
             }
             else
             {
                 commentsRect = new Rect(nodeRect.xMax + 5, nodeRect.yMin, Mathf.Min(size.x, nodeRect.width * 2), nodeRect.height);
             }
 
             GUI.color = new Color(1, 1, 1, 0.6f);
             GUI.backgroundColor = new Color(1f, 1f, 1f, 0.2f);
             GUI.Box(commentsRect, nodeComment, "textArea");
             GUI.backgroundColor = Color.white;
             GUI.color = Color.white;
         }
    }

    //Shows the tag label on the left of the node if it is tagged
    void DrawNodeTag()
    {
        if (!string.IsNullOrEmpty(tag))
        {
            var size = new GUIStyle("label").CalcSize(new GUIContent(tag));
            var tagRect = new Rect(nodeRect.x - size.x - 10, nodeRect.y, size.x, size.y);
            GUI.Label(tagRect, tag);
            tagRect.width = EditorUtils.tagIcon.width;
            tagRect.height = EditorUtils.tagIcon.height;
            tagRect.y += tagRect.height + 5;
            tagRect.x = nodeRect.x - 22;
            GUI.DrawTexture(tagRect, EditorUtils.tagIcon);
        }
    }

    //The inspector of the node shown in the editor panel or else.
    public void ShowNodeInspectorGUI()
    {
        UndoManager.CheckUndo(graph, "Node Inspector");

        if (NCPrefs.showNodeInfo)
        {
            GUI.backgroundColor = new Color(0.8f, 0.8f, 1);
    //        EditorGUILayout.HelpBox(description, MessageType.None);
            GUI.backgroundColor = Color.white;
        }

        GUILayout.BeginHorizontal();
       /* if (!inIconMode && allowAsPrime)
        {
            customName = EditorGUILayout.TextField(customName);
            EditorUtils.TextFieldComment(customName, "Name...");
        }*/

        tag = EditorGUILayout.TextField(tag);
        EditorUtils.TextFieldComment(tag, "Tag...");

        if (!hasColorAttribute)
        {
            nodeColor = EditorGUILayout.ColorField(nodeColor, GUILayout.Width(30));
        }

        GUILayout.EndHorizontal();

        GUI.color = new Color(1, 1, 1, 0.5f);
        nodeComment = EditorGUILayout.TextArea(nodeComment);
        GUI.color = Color.white;
        EditorUtils.TextFieldComment(nodeComment, "Comments...");

        EditorUtils.Separator();
        OnNodeInspectorGUI();
 
        if (GUI.changed)
        { 
            //minimize node so that GUILayour brings it back to correct scale
            nodeRect = new Rect(nodePosition.x, nodePosition.y, Node.minSize.x, Node.minSize.y);
        }

        UndoManager.CheckDirty(graph);
    }

    //Activates/Deactivates all inComming connections
    void SetActive(bool active)
    {
/*
        if (isChecked)
        {
            return;
        }

        isChecked = true;

        //just for visual feedback
        if (!active)
        {
            Graph.currentSelection = null;
        }

        Undo.RecordObject(graph, "SetActive");

        //disable all incomming
        foreach (var cIn in inConnections)
        {
            cIn.isActive = active;
        }

        //disable all outgoing
        foreach (var cOut in outConnections)
        {
            cOut.isActive = active;
        }

        //if child is still considered active(= at least 1 incomming is active), continue else SetActive child as well
        foreach (var child in outConnections.Select(c => c.targetNode))
        {

            if (child.isActive == !active)
            {
                continue;
            }

            child.SetActive(active);
        }

        isChecked = false;*/
    }

    ///Draw an automatic editor inspector for this node.
    protected void DrawDefaultInspector()
    {
        EditorUtils.ShowAutoEditorGUI(this);
    }

    //Een thenode is picked
    protected virtual void OnNodePicked()
    {

    }

    //When the node is released (mouse up)
    protected virtual void OnNodeReleased()
    {

    }

    ///Override to show controls within the node window
    protected virtual void OnNodeGUI()
    {
    }

    //Override to show controls within the inline inspector or leave it to show an automatic editor
    protected virtual void OnNodeInspectorGUI()
    {
        DrawDefaultInspector();
    }

    //Editor. Override to add more entries to the right click context menu of the node
    protected virtual GenericMenu OnContextMenu(GenericMenu menu)
    {
        return menu;
    }

    


    //Draw the connections line from this node, to all of its children. This is the default hierarchical tree style. Override in each system's base node class.
    protected virtual void DrawNodeConnections(Rect drawCanvas, bool fullDrawPass, Vector2 canvasMousePos, float zoomFactor)
    {
        var e = Event.current;

        //Receive connections first
        if (clickedPort != null && e.type == EventType.MouseUp && e.button == 0)
        {
            if (nodeRect.Contains(e.mousePosition))
            {
                graph.ConnectNodes(clickedPort.parent, this, clickedPort.portIndex);
                clickedPort = null;
                e.Use();

            }
            else
            {
                dragDropMisses++;
                if (dragDropMisses == graph.allNodes.Count && clickedPort != null)
                {
                    var source = clickedPort.parent;
                    var index = clickedPort.portIndex;
                    var pos = e.mousePosition;
                    clickedPort = null;

                    System.Action<System.Type> Selected = delegate (System.Type type) {
                        var newNode = graph.AddNode(type, pos);
                        graph.ConnectNodes(source, newNode, index);
                        Graph.currentSelection = newNode;
                    };

                    var menu = EditorUtils.GetTypeSelectionMenu(graph.baseNodeType, Selected);
                    Graph.PostGUI += () => { menu.ShowAsContext(); };
                    e.Use();
                }
            }
        }

        if (maxOutConnections == 0)
        {
            return;
        }

        if (fullDrawPass || drawCanvas.Overlaps(nodeRect))
        {
            var nodeOutputBox = new Rect(nodeRect.x, nodeRect.yMax - 4, nodeRect.width, 12);
            GUI.Box(nodeOutputBox, string.Empty, "nodePortContainer");

            //draw the ports
            if (outConnections.Count < maxOutConnections || maxOutConnections == -1)
            {
                for (var i = 0; i < outConnections.Count + 1; i++)
                {
                    var portRect = new Rect(0, 0, 10, 10);
                    portRect.center = new Vector2(((nodeRect.width / (outConnections.Count + 1)) * (i + 0.5f)) + nodeRect.xMin, nodeRect.yMax + 6);
                    GUI.Box(portRect, string.Empty, (GUIStyle)"nodePortEmpty");

                    //if (Graph.allowClick)
                    {
                        //start a connection by clicking a port
                        EditorGUIUtility.AddCursorRect(portRect, MouseCursor.ArrowPlus);
                        if (e.type == EventType.MouseDown && e.button == 0 && portRect.Contains(e.mousePosition))
                        {
                            dragDropMisses = 0;
                            clickedPort = new GUIPort(i, this, portRect.center);
                            e.Use();
                        }
                    }
                }
            }
        }

        //draw the new drag&drop connection line
        if (clickedPort != null && clickedPort.parent == this)
        {
            var yDiff = (clickedPort.pos.y - e.mousePosition.y) * 0.5f;
            yDiff = e.mousePosition.y > clickedPort.pos.y ? -yDiff : yDiff;
            var tangA = new Vector2(0, yDiff);
            var tangB = tangA * -1;
            Handles.DrawBezier(clickedPort.pos, e.mousePosition, clickedPort.pos + tangA, e.mousePosition + tangB, new Color(0.5f, 0.5f, 0.8f, 0.8f), null, 3);
        }

        //draw all connected lines
        for (var i = 0; i < outConnections.Count; i++)
        {
            var connection = outConnections[i];
            if (connection != null)
            {
                var sourcePos = new Vector2(((nodeRect.width / (outConnections.Count + 1)) * (i + 1)) + nodeRect.xMin, nodeRect.yMax + 6);
                var targetPos = new Vector2(connection.targetNode.nodeRect.center.x, connection.targetNode.nodeRect.y);

                var sourcePortRect = new Rect(0, 0, 12, 12);
                sourcePortRect.center = sourcePos;

                var targetPortRect = new Rect(0, 0, 15, 15);
                targetPortRect.center = targetPos;

                var boundRect = RectUtils.GetBoundRect(sourcePortRect, targetPortRect);
                if (fullDrawPass || drawCanvas.Overlaps(boundRect))
                {
                    GUI.Box(sourcePortRect, string.Empty, (GUIStyle)"nodePortConnected");
                    connection.DrawConnectionGUI(sourcePos, targetPos);

                   // if (Graph.allowClick)
                    {
                        //On right click disconnect connection from the source.
                        if (e.type == EventType.ContextClick && sourcePortRect.Contains(e.mousePosition))
                        {
                            graph.RemoveConnection(connection);
                            e.Use();
                            return;
                        }

                        //On right click disconnect connection from the target.
                        if (e.type == EventType.ContextClick && targetPortRect.Contains(e.mousePosition))
                        {
                            graph.RemoveConnection(connection);
                            e.Use();
                            return;
                        }
                    }
                }
            }
        }
    }

    public virtual void OnInputConnectionConnected(int connectionIndex)
    {

    }

    public virtual void OnInputConnectionDisconnected(int connectionIndex)
    {

    }

    public virtual void OnOutputConnectionConnected(int connectionIndex)
    {

    }

    public virtual void OnOutputConnectionDisconnected(int connectionIndex)
    {

    }

    public bool IsNewConnectionAllowed()
    {
        return IsNewConnectionAllowed(null);
    }

    public virtual bool IsNewConnectionAllowed(NodeTest sourceNode)
    {
        if (sourceNode != null)
        {
            if (this == sourceNode)
            {
                Debug.LogWarning("Node can't connect to itself");
                return false;
            }

            if (sourceNode.outConnections.Count >= sourceNode.maxOutConnections && sourceNode.maxOutConnections != -1)
            {
                Debug.LogWarning("Source node can have no more out connections.");
                return false;
            }
        }

        if (maxInConnections == 1)
        {
            Debug.LogWarning("Target node can have no more connections");
            return false;
        }

        if (maxInConnections <= inConnections.Count && maxInConnections != -1)
        {
            Debug.LogWarning("Target node can have no more connections");
            return false;
        }
        return true;
    }
}