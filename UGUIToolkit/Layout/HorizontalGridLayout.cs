using System;
using UnityEngine;

/// <summary>
/// 水平网格布局, 支持ScrollView水平滑动的时候使用有限的几个item不断复用进行网格布局
/// </summary>
public class HorizontalGridLayout : DynamicLayout, IDynamicLayout  //去除？？？？
{
    protected class ColumnLayoutParam : LayoutParam
    {
        public int column;         //item所属的列
        public int index;          //item的索引
    }

    [SerializeField] protected int  m_Row;              //布局信息,Item显示的行数
    [SerializeField] protected bool m_ExpandHeight;     //强制Item的高度自适应可视区域的高
    [SerializeField] protected bool m_AutoDrag;         //当content的宽度大于viewport的宽度时候自动可拖拽，否则不支持拖拽

    protected int m_FirstVisibleColumn = 0;  //当前区域第一个可见的列, 从0开始
    protected int m_LastVisibleColumn = -1;  //当前区域最后一个可见的列
    protected int m_TotalColumn = 0;         //可显示的总的列数

  //  protected Deque<ColumnLayoutParam> m_ItemView Children = new Deque<ColumnLayoutParam>();
   // protected LayoutParamPool<ColumnLayoutParam> m_LayoutParamPool = new LayoutParamPool<ColumnLayoutParam>(); //item的布局信息缓存池

    public int row
    {
        get { return m_Row; }
        set
        {
            m_Row = Mathf.Clamp(m_Row, 1, int.MaxValue);
            RefreshAllItem();
        }
    }

    public bool expandHeight
    {
        get { return m_ExpandHeight; }
        set
        {
            m_ExpandHeight = value;
            RefreshCurrentItem();
        }
    }

    #region Item操作

    /// <summary>
    /// 刷新所有item，布局也从第一个开始重新布局
    /// </summary>
    public override void RefreshAllItem()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        m_FirstVisibleColumn = 0;  
        m_LastVisibleColumn = -1;
        m_ScrollView.velocity = Vector2.zero;
        RefreshCurrentItem();
    }

    /// <summary>
    /// 刷新当前可见item,通常用于item对应数据被修改，或者有数据被删除时通知刷新UI
    /// </summary>
    public override void RefreshCurrentItem()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        //最好把已有的item全部回收,特别是如果支持多种不同item的不回收，容易复用错误的item
        while (m_ItemViewChildren.Count > 0)
        {
            m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.Dequeue());
        }

        m_FirstVisibleColumn = 0;
        m_LastVisibleColumn = -1;
        CalculateWidth();
        PerformLayout();
    }

    /// <summary>
    /// 刷新index对应item的UI，item必须是可视的item，否则不进行处理
    /// </summary>
    /// <param name="index"></param>
    public override void RefreshItem(int index)
    {
        if (index < 0 || index >= m_Adapter.GetCount())
            return;

        for (int i = 0, count = m_ItemViewChildren.Count; i < count; ++i)
        {
            var layoutInfo = m_ItemViewChildren.GetElement(i);
            var layoutParam = layoutInfo.layoutParam as ColumnLayoutParam;
            if (layoutParam.index == index)
            {
                m_Adapter.ProcessItemView(index, layoutInfo.itemView, this);
                break;
            }
        }
    }

    /// <summary>
    /// 定位到某一个item
    /// </summary>
    /// <param name="itemIndex">item在数据中的索引</param>
    /// <param name="resetStartPos">是否从content的开头开始动画滚动</param>
    /// <param name="useAnimation">是否使用动画滚动, false则为瞬间定位到item</param>
    /// <param name="factor">[0, 1] 0显示的viewport的最左面， 1显示的viewport的最右面， 0-1之间按比例显示在viewport中间</param>
    public void ScrollToItem(int itemIndex, bool resetStartPos = false, bool useAnimation = true, float factor = 0.5f)
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        if (itemIndex < 0 || itemIndex >= m_Adapter.GetCount())
            return;

        var contentMinPos = m_ScrollView.viewport.rect.width - m_ScrollView.content.rect.width;
        if (contentMinPos > 0)
            contentMinPos = 0;
        var contentMaxPos = 0;
      
        var itemWidth = m_Adapter.GetItemSize().x;
        var columnIndex = itemIndex % m_TotalColumn;
        var targetPosX = -padding.left - columnIndex * (itemWidth + spacing.x);

        var deltaWidth = m_ScrollView.viewport.rect.width - itemWidth;
        targetPosX += Mathf.Clamp01(factor) * deltaWidth;
        targetPosX = Mathf.Clamp(targetPosX, contentMinPos, contentMaxPos);

        m_ScrollView.StartAnimation(new Vector2(targetPosX, 0), resetStartPos, useAnimation);
    }

    #endregion

    #region 布局处理

    /// <summary>
    /// 监听ScrollView的Content位置更改事件, 根据位置重新计算布局
    /// </summary>
    /// <param name="oldPos"></param>
    /// <param name="newPos"></param>
    public override void OnContentPositionChanged(Vector2 oldPos, Vector2 newPos)
    {
        if (m_ScrollView == null || m_Adapter == null || m_Adapter.IsEmpty())
            return;

        if (!m_ScrollView.horizontal || m_ScrollView.vertical)
        {
            Debug.LogError("HorizontalGridLayout只支持ScrollView为水平滑动的时候才生效");
            return;
        }

        PerformLayout();
    }

    /// <summary>
    /// 计算ScrollView的Content的宽度
    /// </summary>
    protected void CalculateWidth()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        var itemCount = m_Adapter.GetCount();
        var itemWidth = m_Adapter.GetItemSize().x;
        m_TotalColumn = itemCount / row;
        m_TotalColumn += itemCount % row > 0 ? 1 : 0;

        var contentWidth = m_TotalColumn * itemWidth + (m_TotalColumn - 1) * spacing.y + padding.horizontal;
        var viewportWidth = m_ScrollView.viewport.rect.width;
        if (contentWidth < viewportWidth)
            contentWidth = viewportWidth;

        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, contentWidth);
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, m_ScrollView.viewport.rect.height);

        //计算当前在viewport右边缘处content的X坐标
        var viewportRightX = m_ScrollView.viewport.rect.width - m_ScrollView.content.anchoredPosition.x;
        if (viewportRightX > contentWidth)
        {
            var oldPos = m_ScrollView.content.anchoredPosition;
            oldPos.x = Mathf.Min(0, m_ScrollView.viewport.rect.width - contentWidth);
            m_ScrollView.content.anchoredPosition = oldPos;
        }
    }

    /// <summary>
    /// 执行布局处理,回收不显示的item, 添加需要显示的item到可视列表中
    /// </summary>
    protected void PerformLayout()
    {
        if (m_ScrollView == null)
            return;

        int newFirstVisibleColumn = 0;
        int newLastVisibleColumn = 0;
        GetVisibleColumnRange(ref newFirstVisibleColumn, ref newLastVisibleColumn);
        //新的可视列区域和旧的不同时,重新计算需要显示的item
        if (newFirstVisibleColumn != m_FirstVisibleColumn || newLastVisibleColumn != m_LastVisibleColumn)
        {
            //如果新的第一个可视列大于旧的,从队列头部移除需要隐藏的item
            if (m_FirstVisibleColumn < newFirstVisibleColumn)
            {
                while (m_ItemViewChildren.Count > 0)
                {
                    var itemView = m_ItemViewChildren.PeekFirst();
                    var layoutParam = itemView.layoutParam as ColumnLayoutParam;
                    if (layoutParam.column >= newFirstVisibleColumn)
                    {
                        break;
                    }
                    m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.DequeueFirst());
                }
            }

            //如果新的最后一个可视列小于旧的,从队列尾部移除需要隐藏的item
            if (m_LastVisibleColumn > newLastVisibleColumn)
            {
                while (m_ItemViewChildren.Count > 0)
                {
                    var itemView = m_ItemViewChildren.PeekLast();
                    var layoutParam = itemView.layoutParam as ColumnLayoutParam;
                    if (layoutParam.column <= newLastVisibleColumn)
                    {
                        break;
                    }
                    m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.DequeueLast());
                }
            }

            var rowCount = row;                         //行数,row是个属性提前在这算下,避免循环中多次计算
            var itemCount = m_Adapter.GetCount();       //item的总数,提前在这算下,避免循环中多次计算

            var widthDelta = m_Adapter.GetItemSize().x + spacing.x;
            var heightDelta = (m_ScrollView.content.rect.height + spacing.y - padding.vertical) / rowCount;
            var forceItemHeight = widthDelta - spacing.x;

            //如果新的第一个可视列小于旧的,从队列头部添加新的item
            if (m_FirstVisibleColumn > newFirstVisibleColumn)
            {
                var lastAddItemColumn = m_FirstVisibleColumn - 1;
                if (newLastVisibleColumn < lastAddItemColumn)
                {
                    lastAddItemColumn = newLastVisibleColumn;
                }

                //添加新的item
                for (int i = lastAddItemColumn; i >= newFirstVisibleColumn; --i)
                {
                    for (int j = 0; j < rowCount; ++j)
                    {
                        var itemIndex = i + j * m_TotalColumn;
                        if (itemIndex >= itemCount)
                            continue;

                        var layoutInfo = m_RecycleBin.GetLayoutInfo();
                        if (layoutInfo == null)
                        {
                            layoutInfo = new LayoutInfo();
                            layoutInfo.itemView = m_Adapter.GetItemView(m_ScrollView.content.gameObject);
                            layoutInfo.layoutParam = new ColumnLayoutParam();
                        }

                        var itemView = layoutInfo.itemView;
                        var layoutParam = layoutInfo.layoutParam as ColumnLayoutParam;
                        SetItemVisible(itemView.rectTransform, true);
                        if(layoutParam == null)
                            throw new NullReferenceException("layoutParam");
                        layoutParam.column = i;    //把列信息存进去
                        layoutParam.index = itemIndex;
                        m_Adapter.ProcessItemView(itemIndex, itemView, this);
                        m_ItemViewChildren.EnqueueFirst(layoutInfo);
                        SetItemPosition(j, i, itemView.rectTransform, heightDelta, widthDelta, forceItemHeight);
                    }
                }
            }

            //如果新的最后一个可视列大于旧的,从队列尾部添加新的item
            if (m_LastVisibleColumn < newLastVisibleColumn)
            {
                var firstAddItemColumn = m_LastVisibleColumn + 1;
                if (newFirstVisibleColumn > firstAddItemColumn)
                {
                    firstAddItemColumn = newFirstVisibleColumn;
                }

                //添加新的item
                for (int i = firstAddItemColumn; i <= newLastVisibleColumn; ++i)
                {
                    for (int j = 0; j < rowCount; ++j)
                    {
                        var itemIndex = i + j * m_TotalColumn;
                        if (itemIndex >= itemCount)
                            continue;

                        var layoutInfo = m_RecycleBin.GetLayoutInfo();
                        if (layoutInfo == null)
                        {
                            layoutInfo = new LayoutInfo();
                            layoutInfo.itemView = m_Adapter.GetItemView(m_ScrollView.content.gameObject);
                            layoutInfo.layoutParam = new ColumnLayoutParam();
                        }

                        var itemView = layoutInfo.itemView;
                        var layoutParam = layoutInfo.layoutParam as ColumnLayoutParam;
                        SetItemVisible(itemView.rectTransform, true);
                        if (layoutParam == null)
                            throw new NullReferenceException("layoutParam");
                        layoutParam.column = i;    //把列信息存进去
                        layoutParam.index = itemIndex;   
                        m_Adapter.ProcessItemView(itemIndex, itemView, this);
                        m_ItemViewChildren.EnqueueLast(layoutInfo);
                        SetItemPosition(j, i, itemView.rectTransform, heightDelta, widthDelta, forceItemHeight);
                    }
                }
            }

            m_FirstVisibleColumn = newFirstVisibleColumn;
            m_LastVisibleColumn = newLastVisibleColumn;
        }
    }

    /// <summary>
    /// 获取当前可视的列范围
    /// </summary>
    protected void GetVisibleColumnRange(ref int firstVisibleColumn, ref int lastVisibleColumn)
    {
        //content的左边距离viewport左边的宽度
        var deltaWidthToLeft = -m_ScrollView.content.anchoredPosition.x - padding.left;
        var itemWidth = m_Adapter.GetItemSize().x + spacing.x;
        firstVisibleColumn = Mathf.FloorToInt(deltaWidthToLeft / itemWidth);
        if (firstVisibleColumn < 0)
        {
            firstVisibleColumn = 0;
        }

        //content的左边距离viewport右边的宽度
        var deltaWidthToRight = m_ScrollView.viewport.rect.width + (deltaWidthToLeft < 0 ? 0 : deltaWidthToLeft);  //如果小于viewport的宽度,则设置为viewport的宽度
        lastVisibleColumn = Mathf.FloorToInt(deltaWidthToRight / itemWidth);
        lastVisibleColumn = Mathf.Clamp(lastVisibleColumn, 0, m_TotalColumn - 1);
    }

    /// <summary>
    /// 设置Item的位置
    /// </summary>
    protected void SetItemPosition(int rowIndex, int columnIndex, RectTransform rectTransform, float heightDelta, float widthDelta, float forceItemHeight)
    {
        //竖直布局
        if (m_ExpandHeight)
        {
            SetChildAlongAxis(rectTransform, 1, padding.top + rowIndex * heightDelta, forceItemHeight);
        }
        else
        {
            SetChildAlongAxis(rectTransform, 1, padding.top + rowIndex * heightDelta);
        }

        //水平布局
        SetChildAlongAxis(rectTransform, 0, padding.left + columnIndex * widthDelta);
    }

    #endregion
}