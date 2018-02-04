using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using JsonFx.U3DEditor;
using NodeCanvas.Editor;
using NodeCanvas.Framework;
using ParadoxNotion;
using ParadoxNotion.Design;
using UnityEditor;
using UnityEngine;

public abstract class GraphTest : BaseGraph
{
    [SerializeField]
    private string m_Comments = string.Empty;

    [SerializeField]
    private Vector2 m_Translation = new Vector2(-5000,-5000);

    [SerializeField]
    private float m_ZoomFactor = 1f;

    [SerializeField]
    private List<NodeTest> m_Nodes = new List<NodeTest>();

    [SerializeField]
    private int m_AutoId;

    //private List<CanvasGroup> _canvasGroups = null;

    private Rect inspectorRect = new Rect(15, 55, 0, 0);
    private Vector2 nodeInspectorScrollPos;

    public int keyboardControl;

    public Vector2 translation
    {
        get { return m_Translation; }
        set
        {
            m_Translation = value;
        }
    }

    public float zoomFactor
    {
        get { return m_ZoomFactor; }
        set { m_ZoomFactor = value; }
    }

    public List<NodeTest> allNodes
    {
        get { return m_Nodes; }
        protected set { m_Nodes = value; }
    }

    public Action PostGUI { get; set; }
    public abstract Type baseNodeType { get; }

    public T AddNode<T>() where T : NodeTest
    {
        return (T)AddNode(typeof(T));
    }

    public T AddNode<T>(Vector2 pos) where T : NodeTest
    {
        return (T)AddNode(typeof(T), pos);
    }

    public NodeTest AddNode(Type nodeType)
    {
        return AddNode(nodeType, new Vector2(50, 50));
    }

    public NodeTest AddNode(Type nodeType, Vector2 pos)
    {
        if (!nodeType.RTIsSubclassOf(baseNodeType))
        {
            Debug.LogWarning(nodeType + " can't be added to " + GetType().FriendlyName() + " graph");
            return null;
        }

        var newNode = NodeTest.Create(this, nodeType, pos);
        Undo.RecordObject(this, "New Node");

        newNode.id = ++m_AutoId;
        allNodes.Add(newNode);
        return newNode;
    }

    public void RemoveNode(int nodeId, bool recordUndo = true)
    {
        var node = GetNode(nodeId);
        if (node != null)
        {
            RemoveNode(node, recordUndo);
        }
    }

    public void RemoveNode(NodeTest node, bool recordUndo = true)
    {
        if (!allNodes.Contains(node))
        {
            Debug.LogWarning("Node is not part of this graph");
            return;
        }

        currentSelection = -1;
        node.OnDestroy();

        //disconnect parents
        foreach (var inConnection in node.inConnections.ToArray())
        {
            RemoveConnection(inConnection);
        }

        //disconnect children
        foreach (var outConnection in node.outConnections.ToArray())
        {
            RemoveConnection(outConnection);
        }

        if (recordUndo)
        {
            Undo.RecordObject(this, "Delete Node");
        }

        allNodes.Remove(node);
    }

    ///Removes a connection
    public void RemoveConnection(ConnectionTest connection, bool recordUndo = true)
    {
        if (recordUndo)
        {
            Undo.RecordObject(this, "Delete Connection");
        }

        connection.OnDestroy();
        connection.sourceNode.OnOutputConnectionDisconnected(connection.sourceNode.outConnections.IndexOf(connection));
        connection.targetNode.OnInputConnectionDisconnected(connection.targetNode.inConnections.IndexOf(connection));

        connection.sourceNode.outConnections.Remove(connection);
        connection.targetNode.inConnections.Remove(connection);

        currentSelection = -1;
    }

    public void ShowNodesGUI(Event e, Rect drawCanvas, bool fullDrawPass, Vector2 canvasMousePos, float zoomFactor)
    {
        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;

        for (var i = 0; i < allNodes.Count; i++)
        {
            allNodes[i].ShowNodeGUI(drawCanvas, fullDrawPass, canvasMousePos, zoomFactor);
        }
    }

    public void ShowGraphControls(Event e, Vector2 canvasMousePos)
    { 
        ShowInspectorGUIPanel(e, canvasMousePos);
        HandleEvents(e, canvasMousePos);
        //AcceptDrops(e, canvasMousePos);

        if (PostGUI != null)
        {
            PostGUI();
            PostGUI = null;
        }
    }

    private NodeTest selectedNode
    {
        get
        {
            return GetNode(currentSelection);
        }
    }

    private  ConnectionTest selectedConnection
    {
        get { return GetConnection(currentSelection); }
    }

    protected NodeTest GetNode(int id)
    {
        foreach (var node in allNodes)
        {
            if (node.id == currentSelection)
            {
                return node; ;
            }
        }
        return null;
    }

    protected ConnectionTest GetConnection(int id)
    {
        foreach (var node in allNodes)
        {
            foreach (var conn in node.inConnections)
            {
                if (conn.id == id)
                {
                    return conn;
                }
            }

            foreach (var conn in node.outConnections)
            {
                if (conn.id == id)
                {
                    return conn;
                }
            }
        }

        return null;
    }

    void ShowInspectorGUIPanel(Event e, Vector2 canvasMousePos)
    {
        if (selectedNode == null && selectedConnection == null)
        {
            inspectorRect.height = 0;
            return;
        }

        inspectorRect.width = 330;
        inspectorRect.x = 10;
        inspectorRect.y = 52;

        EditorGUIUtility.AddCursorRect(new Rect(inspectorRect.x, inspectorRect.y, 330, 30), MouseCursor.Link);

        if (GUI.Button(new Rect(inspectorRect.x+2, inspectorRect.y, 330, 30), ""))
        {
            NCPrefs.showNodePanel = !NCPrefs.showNodePanel;
        }

        GUI.Box(inspectorRect, "", "windowShadow");
        var title = selectedNode != null ? selectedNode.name : "Connection";
        if (NCPrefs.showNodePanel)
        {
            var lastSkin = GUI.skin;
            var viewRect = new Rect(inspectorRect.x, inspectorRect.y, inspectorRect.width + 18, Screen.height - inspectorRect.y - 30);
            nodeInspectorScrollPos = GUI.BeginScrollView(viewRect, nodeInspectorScrollPos, inspectorRect);

            inspectorRect.x += 2;
            GUILayout.BeginArea(inspectorRect, title, "editorPanel");
            GUILayout.Space(5);
            GUI.skin = null;

            if (selectedNode != null)
            {
                selectedNode.ShowNodeInspectorGUI();
            }
            else if (selectedConnection != null)
            {
                selectedConnection.ShowConnectionInspectorGUI();
            }

            GUILayout.Box("", GUILayout.Height(5), GUILayout.Width(inspectorRect.width - 10));
            GUI.skin = lastSkin;
            if (e.type == EventType.Repaint)
            {
                inspectorRect.height = GUILayoutUtility.GetLastRect().yMax + 5;
            }

            GUILayout.EndArea();
            GUI.EndScrollView();

            if (GUI.changed)
            {
                EditorUtility.SetDirty(this);
            }
        }
        else
        {
            inspectorRect.x += 2;
            GUI.Box(inspectorRect, title, "editorPanel");
            inspectorRect.height = 55;
            GUI.color = new Color(1, 1, 1, 0.2f);
            GUI.Box(new Rect(inspectorRect.x, inspectorRect.y + 30, inspectorRect.width, 20), "...");
            GUI.color = Color.white;
        }
    }

    protected virtual void HandleEvents(Event e, Vector2 canvasMousePos)
    {
        //we also undo graph pans
        if (e.type == EventType.MouseDown && e.button == 2)
        {
            Undo.RegisterCompleteObjectUndo(this, "Graph Pan");
        }
 
        var inspectorWithScrollbar = new Rect(inspectorRect.x, inspectorRect.y, inspectorRect.width + 14, inspectorRect.height);
        if (inspectorWithScrollbar.Contains(e.mousePosition))
        {
            return;
        }

        //Shortcuts
        if (e.type == EventType.KeyUp && GUIUtility.keyboardControl == keyboardControl)
        {
            //Delete
            if (e.keyCode == KeyCode.Delete || e.keyCode == KeyCode.Backspace)
            {
                if (multiSelection != null && multiSelection.Count > 0)
                {
                    foreach (var id in multiSelection.ToArray())
                    {
                        var node = GetNode(id);
                        if (node != null)
                        {
                            RemoveNode(node);
                        }

                        var conn = GetConnection(id);
                        if (conn != null)
                        {
                            RemoveConnection(conn);
                        }
                    }
                    multiSelection = null;
                }

                if (selectedNode != null)
                {
                    RemoveNode(selectedNode);
                    currentSelection = -1;
                }

                if (selectedConnection != null)
                {
                    RemoveConnection(selectedConnection);
                    currentSelection = -1;
                }
                e.Use();
            }

            //Duplicate
            if (e.keyCode == KeyCode.D && e.control)
            {
                /*if (multiSelection != null && multiSelection.Count > 0)
                {
                    var newNodes = CopyNodesToGraph(multiSelection.OfType<Node>().ToList(), this);
                    multiSelection = newNodes.Cast<object>().ToList();
                }
                if (selectedNode != null)
                {
                    currentSelection = selectedNode.Duplicate(this);
                }*/
                //Connections can't be duplicated by themselves. They do so as part of multiple node duplication though.
                e.Use();
            }
        }

        //Right click canvas context menu. Basicaly for adding new nodes.
        if (e.type == EventType.ContextClick)
        {
            var menu = GetAddNodeMenu(canvasMousePos);
            if (Node.copiedNodes != null && Node.copiedNodes[0].GetType().IsSubclassOf(baseNodeType))
            {
                menu.AddSeparator("/");
                if (Node.copiedNodes.Length == 1)
                {
                    menu.AddItem(new GUIContent(string.Format("Paste Node ({0})", Node.copiedNodes[0].GetType().Name)), false, () =>
                    {
                   /*     var newNode = Node.copiedNodes[0].Duplicate(this);
                        newNode.nodePosition = canvasMousePos;
                        currentSelection = newNode;*/
                    });

                }
                else if (Node.copiedNodes.Length > 1)
                {
                    menu.AddItem(new GUIContent(string.Format("Paste Nodes ({0})", Node.copiedNodes.Length.ToString())), false, () =>
                    {
                      /*  var newNodes = Graph.CopyNodesToGraph(Node.copiedNodes.ToList(), this);
                        var diff = newNodes[0].nodeRect.center - canvasMousePos;
                        newNodes[0].nodePosition = canvasMousePos;
                        for (var i = 1; i < newNodes.Count; i++)
                        {
                            newNodes[i].nodePosition -= diff;
                        }
                        multiSelection = newNodes.Cast<object>().ToList();*/
                    });
                }
            }

            menu.ShowAsContext();
            e.Use();
        }
    }

    [SerializeField]
    private  List<int> m_MultiSelectionIds = new List<int>();

    [SerializeField]
    private int m_CurrentSelection;

    public  List<int> multiSelection
    {
        get { return m_MultiSelectionIds; }
        set
        {
            if (value != null && value.Count == 1)
            {
                currentSelection = value[0];
                value.Clear();
            }
            m_MultiSelectionIds = value != null ? value : new List<int>();
        }
    }

    public int currentSelection
    {
        get
        {
            if (m_MultiSelectionIds.Count > 1)
            {
                return -1;
            }

            if (m_MultiSelectionIds.Count == 1)
            {
                return m_MultiSelectionIds[0];
            }

            return m_CurrentSelection;
        }
        set
        {
            if (!multiSelection.Contains(value))
            {
                multiSelection.Clear();
            }
            m_CurrentSelection = value;
        }
    }

    /// <summary>
    /// ???????????????????????????????????
    /// </summary>
    /// <param name="canvasMousePos"></param>
    /// <returns></returns>
    protected GenericMenu GetAddNodeMenu(Vector2 canvasMousePos)
    {
        Action<Type> selected = (type) => { currentSelection = AddNode(type, canvasMousePos).id; };
        //这里要改，好像不支持editor的结点
        var menu = EditorUtils.GetTypeSelectionMenu(baseNodeType, selected);
        menu = OnCanvasContextMenu(menu, canvasMousePos);
        return menu;
    }

    protected virtual GenericMenu OnCanvasContextMenu(GenericMenu menu, Vector2 canvasMousePos)
    {
        return menu;
    }

    public ConnectionTest ConnectNodes(NodeTest sourceNode, NodeTest targetNode)
    {
        return ConnectNodes(sourceNode, targetNode, sourceNode.outConnections.Count);
    }

    public ConnectionTest ConnectNodes(NodeTest sourceNode, NodeTest targetNode, int indexToInsert)
    {
        if (targetNode.IsNewConnectionAllowed(sourceNode) == false)
        {
            return null;
        }
 
        Undo.RecordObject(this, "New Connection");

        var newConnection = ConnectionTest.Create(sourceNode, targetNode, indexToInsert);
        newConnection.id = ++m_AutoId;
        sourceNode.OnOutputConnectionConnected(indexToInsert);
        targetNode.OnInputConnectionConnected(targetNode.inConnections.IndexOf(newConnection));
        return newConnection;
    }

    public static GraphTest LoadGraph(bool lazyMode = true, string filePath = null, string assetPath = null)
    {
        if (!string.IsNullOrEmpty(filePath))
        { 
            filePath = Path.GetFullPath(filePath);
            if (File.Exists(filePath))
            {
                var data = File.ReadAllBytes(filePath);
                var graph = JsonReader.Deserialize(Encoding.UTF8.GetString(Decompress(data)), true) as GraphTest;
                if (graph != null)
                {
                    graph.FilePath = filePath;
                    graph.m_LazyMode = lazyMode;
                    return graph;
                }
            }
        }

        if (!string.IsNullOrEmpty(assetPath))
        {
            var graph = Resources.Load<GraphTest>(assetPath);
            
            if (graph == null)
            {
                graph = AssetDatabase.LoadAssetAtPath<GraphTest>(assetPath);
            }

            if (graph != null)
            {
                var jsonReaderSettings = new JsonReaderSettings();
                jsonReaderSettings.HandleCyclicReferences = true;
                if (graph.m_Data != null && graph.m_Data.Length > 0)
                {
                    var jsonReader = new JsonReader(Encoding.UTF8.GetString(Decompress(graph.m_Data)), jsonReaderSettings);
                    jsonReader.autoType = true;
                    jsonReader.PopulateObject(ref graph);
                }
                graph.m_LazyMode = lazyMode;
                graph.AssetPath = assetPath;
                return graph;
            }
        }
        return null;
    }
}
