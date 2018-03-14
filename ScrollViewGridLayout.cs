using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ScrollViewGridLayout : AbstractScrollViewLayout
{
    [SerializeField] private int m_Row;
    [SerializeField] private int m_Column;
    [SerializeField] private Vector2 m_Divider;
    [SerializeField] private RectTransform m_ItemRectTransform;

    //这里应该重新布局了,高度什么的也要变啊
    public int Row
    {
        get { return m_Row; }
        set { m_Row = value; }
    }

    //这里应该重新布局了,高度什么的也要变啊
    public int Column
    {
        get { return m_Column; }
        set { m_Column = value; }
    }

    //这里应该重新布局了,高度什么的也要变啊
    public Vector2 Divider
    {
        get { return m_Divider; }
        set { m_Divider = value; }
    }

    //改变了就全部刷新
    public RectTransform ItemRectTransform
    {
        get { return m_ItemRectTransform; }
        set { m_ItemRectTransform = value; }
    }

    private void Start()
    {
        if(ItemRectTransform != null)
            ItemRectTransform.gameObject.SetActive(false);
    }

    private RectTransform content;

    [SerializeField] protected Vector2 m_CellSize = new Vector2(100, 100);
    public Vector2 cellSize { get { return m_CellSize; } set { SetProperty(ref m_CellSize, value); } }
    private readonly Vector3[] m_Corners = new Vector3[4];


    public override void OnContentPositionChanged(ScrollView scrollView, Bounds viewBounds, Bounds contentBounds)
    {
        if (scrollView == null)
            return;

        //当前只支持竖直的情况
        if (!scrollView.vertical || scrollView.horizontal)
            return;

        content = scrollView.content;

        content.GetWorldCorners(m_Corners);

        for (int i = 0; i < m_Corners.Length; i++)
        {
            Debug.LogError("===="+m_Corners[i]);
        }

        scrollView.viewport.GetWorldCorners(m_Corners);

        for (int i = 0; i < m_Corners.Length; i++)
        {
            Debug.LogError("====---" + m_Corners[i]);
        }

        Debug.LogError(content.anchoredPosition);
    }


    //private List<IItemView> m_ItemViewChildren = new List<IItemView>();
    //m_FirstPosition
    //使用双端队列

    public void NotifyItemCountChanged()
    {
        
        content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 2000);
    }






    public override void CalculateLayoutInputVertical()
    {
    //    throw new System.NotImplementedException();
    }

    public override void SetLayoutHorizontal()
    {

    }

    public override void SetLayoutVertical()
    {
    //    throw new System.NotImplementedException();
    }
}
