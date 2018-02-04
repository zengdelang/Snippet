using System;
using ParadoxNotion.Design;
using UnityEditor;
using UnityEngine;


public class Graph1 : GraphTest
{
    public override Type baseNodeType
    {
        get { return typeof(Node1); }
    }
}

public abstract class Node1 : NodeTest
{
    public override System.Type outConnectionType { get { return typeof(Connection1); } }
}

[Category("Composites")]
[Description("Works like a normal Selector, but when a child node returns Success, that child will be moved to the end.\nAs a result, previously Failed children will always be checked first and recently Successful children last")]
[Icon("FlipSelector")]
[Color("b3ff7f")]
public class Node11 : Node1
{
    [SerializeField]
    protected string haha;

    sealed public override int maxOutConnections { get { return -1; } }

     

    public override int maxInConnections
    {
        get { return -1; }
    }
}

public class Node12 : Node1
{
    [SerializeField]
    protected string haha;


    sealed public override int maxOutConnections { get { return 5; } }

 

    public override int maxInConnections
    {
        get { return 1; }
    }
}

public class Connection1 : ConnectionTest
{
    
}



public class GraphEditorWindow : ViewGroupEditorWindow
{
    [MenuItem("Tools/Eaxamples/GraphEditorWindow", false, 0)]
    public static void ShowCoreConfigTool()
    {
        GetWindow<GraphEditorWindow>();
       
    }

    protected override void InitData()
    {
        ViewGroup viewGroup = new ViewGroup(m_LayoutGroupMgr);
        var graphView = new GraphView(m_LayoutGroupMgr);
        graphView.currentGraph = new Graph1();
        var searchBar = new SearchBar(m_LayoutGroupMgr);
        viewGroup.AddView(searchBar);
        viewGroup.AddView(graphView);
        m_LayoutGroupMgr.AddViewGroup(viewGroup);
    }
}
