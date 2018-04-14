using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public interface IItemView
{
    int extra { get; set; }  //ItemView的额外信心，由布局使用，上层逻辑不应该使用或修改这个值
    RectTransform rectTransform { get; set; }
}

public interface IDynamicLayout
{
}

public interface IFixedSizeItemAdapter
{
    /// <summary>
    /// 需要展示的数据的总数目
    /// </summary>
    /// <returns></returns>
    int GetCount();

    /// <summary>
    /// 适配器数据数目是否为0
    /// </summary>
    /// <returns></returns>
    bool IsEmpty();

    /// <summary>
    /// 得到Item的UI大小
    /// </summary>
    /// <returns></returns>
    Vector2 GetItemSize();

    /// <summary>
    /// 得到ItemView的信息,ItemView用于保存UI控件数据,用于ProcessItemView中直接得到对应ItemView的UI控件
    /// </summary>
    /// <param name="itemParent">新生成的Item要挂载的父节点</param>
    /// <returns></returns>
    IItemView GetItemView(GameObject itemParent);

    /// <summary>
    /// 处理Item的ui逻辑
    /// </summary>
    /// <param name="position">当前需要处理的数据在总数据中的索引,默认从0开始</param>
    /// <param name="itemView">当前item对应的itemView, 保存每一个item的信息, 一般保存item对应的ui控件, 由GetItemView设置信息</param>
    /// <param name="parent">当前使用该适配器的布局</param>
    void ProcessItemView(int position, IItemView itemView, IDynamicLayout parent);

    /// <summary>
    /// 当布局组件被禁用或者删除的时候时候调用，可用于一些通用ui控件的回收，比如有一个通用的ui用于显示道具
    /// 这个通用的ui有一个自己的缓存池，当RecycleItemView的时候可以考虑把通用ui相关的ui控件回收到它自己的缓冲池
    /// </summary>
    /// <param name="itemView"></param>
    void RecycleItemView(IItemView itemView);
}

/// <summary>
/// 竖直网格布局, 支持ScrollView竖直滑动的时候使用有限的几个item不断复用进行网格布局
/// </summary>
public class VerticalGridLayout : MonoBehaviour, IDynamicLayout
{
    [Serializable]
    public class BoolUnityEvent : UnityEvent<bool>
    {
    }

    [SerializeField] protected RectOffset m_Padding = new RectOffset();

    [SerializeField] protected int m_Column = 1; //布局信息,Item显示的列数

    [SerializeField] protected Vector2 m_Spacing = Vector2.zero; //布局信息,Item和Item之间的水平和竖直间隔

    [SerializeField] protected ScrollView m_ScrollView; //竖直滑动的ScrollView, 布局监听ScrollView的onContentPosChanged来动态布局






    [SerializeField] protected bool m_ExpandWidth; //强制Item的宽度自适应可视区域的宽,竖直滑动的时候有效

    [SerializeField] protected bool m_AutoDrag; //当content的高度大于viewport的高度时候自动可拖拽，否则不支持拖拽

    [SerializeField] protected bool m_EnableCanLockEvent; //是否开启锁定状态通知，可锁定状态下有新数据来可以提示而不直接刷新ScrollView
    [SerializeField] protected BoolUnityEvent m_CanLockEvent = new BoolUnityEvent(); //可锁定状态指示事件
    [NonSerialized] protected bool m_IsLock; //是否是锁定状态，锁定状态下外部处理不应该在有新数据的时候立即刷新UI，可能会导致频繁的UI刷新，影响UI查看

    [SerializeField] protected bool m_EnableLoadMoreEvent; //是否开启加载更多事件，加载更多可用于分页请求数据
    [SerializeField] protected UnityEvent m_LoadMoreEvent = new UnityEvent(); //加载更多事件
    [SerializeField] protected float m_LoadMoreCD = 0.1f;
    [NonSerialized] protected float m_CurrentTime = -1;

    [SerializeField] protected bool m_EnableArrowHintEvent; //是否开启箭头指示事件, 可选用指示当前viewport向上或向下拖拽是否有内容可拖拽
    [SerializeField] protected BoolUnityEvent m_ArrowUpEvent = new BoolUnityEvent(); //向上箭头指示
    [SerializeField] protected BoolUnityEvent m_ArrowDownEvent = new BoolUnityEvent(); //向下箭头指示

    protected DrivenRectTransformTracker m_Tracker;
    protected int m_FirstPosition; //当前第一个可视的Item在所有数据中的索引位置
    protected IFixedSizeItemAdapter m_Adapter; //布局所使用的适配器
    protected RecycleBin m_RecycleBin = new RecycleBin(); //ItemView的缓存池
    protected Deque<IItemView> m_ItemViewChildren = new Deque<IItemView>(); //保存可视的itemView的双端队列

    /// <summary>
    ///   <para>The padding to add around the child layout elements.</para>
    /// </summary>
    public RectOffset padding
    {
        get
        {
            return m_Padding;
        }
        set
        {
            m_Padding = value;
            RefreshAllItem();
        }
    }

    public Vector2 spacing
    {
        get { return m_Spacing; }
        set
        {
            if (m_Spacing != value)
            {
                m_Spacing = value;
                RefreshAllItem();
            }
        }
    }

    public int column
    {
        get { return m_Column; }
        set
        {
            m_Column = Mathf.Clamp(m_Column, 1, int.MaxValue);
            RefreshAllItem();
        }
    }

    public bool expandWidth
    {
        get { return m_ExpandWidth; }
        set
        {
            m_ExpandWidth = value;
            RefreshCurrentItem();
        }
    }

    public bool autoDrag
    {
        get { return m_AutoDrag; }
        set
        {
            m_AutoDrag = value;
            CheckAutoDrag();
        }
    }

    public bool enableCanLockEvent
    {
        get { return m_EnableCanLockEvent; }
        set { m_EnableCanLockEvent = value; }
    }

    public BoolUnityEvent canLockEvent
    {
        get { return m_CanLockEvent; }
        set { m_CanLockEvent = value; }
    }

    public bool isLock
    {
        get { return m_IsLock; }
        set
        {
            if (m_EnableCanLockEvent)
            {
                if (m_IsLock != value)
                {
                    m_IsLock = value;
                    m_CanLockEvent.Invoke(value);
                    if (!value)
                        RefreshAllItem();
                }
            }
        }
    }

    public bool enableLoadMoreEvent
    {
        get { return m_EnableLoadMoreEvent; }
        set { m_EnableLoadMoreEvent = value; }
    }

    public UnityEvent loadMoreEvent
    {
        get { return m_LoadMoreEvent; }
        set { m_LoadMoreEvent = value; }
    }

    public float loadMoreCD
    {
        get { return m_LoadMoreCD; }
        set { m_LoadMoreCD = value; }
    }

    public bool enableArrowHintEvent
    {
        get { return m_EnableArrowHintEvent; }
        set { m_EnableArrowHintEvent = value; }
    }

    public BoolUnityEvent arrowUpEvent
    {
        get { return m_ArrowUpEvent; }
        set { m_ArrowUpEvent = value; }
    }

    public BoolUnityEvent arrowDownEvent
    {
        get { return m_ArrowDownEvent; }
        set { m_ArrowDownEvent = value; }
    }

    protected void OnEnable()
    {
        SetupPivotAndAnchor();
        RefreshCurrentItem();
    }

    protected void OnDisable()
    {
        m_Tracker.Clear();
    }

    /// <summary>
    /// 设置content的Pivor和Anchor方便布局计算
    /// </summary>
    protected void SetupPivotAndAnchor()
    {
        if (m_ScrollView != null)
        {
            m_Tracker.Add(this, m_ScrollView.content,
                DrivenTransformProperties.Pivot | DrivenTransformProperties.AnchorMin |
                DrivenTransformProperties.AnchorMax);
            m_ScrollView.content.pivot = new Vector2(0, 1);
            m_ScrollView.content.anchorMin = new Vector2(0, 1);
            m_ScrollView.content.anchorMax = new Vector2(0, 1);
        }
    }

    /// <summary>
    /// 监听ScrollView的Content位置更改事件, 根据位置重新计算布局
    /// </summary>
    /// <param name="oldPos"></param>
    /// <param name="newPos"></param>
    public void OnContentPositionChanged(Vector2 oldPos, Vector2 newPos)
    {
        if (m_ScrollView == null || m_Adapter == null || m_Adapter.IsEmpty())
            return;

        if (!m_ScrollView.vertical || m_ScrollView.horizontal)
        {
            Debug.LogError("VerticalGridLayout只支持ScrollView为竖直滑动的时候才生效");
            return;
        }

        CalculateLayoutInfo();
        CheckArrowHint();
        CheckCanLockState();
        CheckLoadMore(newPos.y > oldPos.y);
    }

    /// <summary>
    /// 设置要监听的ScrollView
    /// </summary>
    /// <param name="scrollView"></param>
    public void SetScrollView(ScrollView scrollView)
    {
        m_ScrollView = scrollView;
        SetupPivotAndAnchor();
        RefreshAllItem();
    }

    /// <summary>
    /// 设置数据适配器
    /// </summary>
    /// <param name="adapter"></param>
    public void SetAdapter(IFixedSizeItemAdapter adapter)
    {
        m_Adapter = adapter;
        RefreshAllItem();
    }

    /// <summary>
    /// 刷新所有item，布局也从content上面开始重新布局
    /// </summary>
    public void RefreshAllItem()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        m_FirstPosition = 0;
        m_ScrollView.content.anchoredPosition = Vector2.zero;
        m_ScrollView.velocity = Vector2.zero;
        RefreshCurrentItem();
    }

    /// <summary>
    /// 刷新当前可见item,通常用于item对应数据被修改，或者有数据被删除时通知刷新UI
    /// </summary>
    public void RefreshCurrentItem()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        //最好把已有的item全部回收,特别是如果支持多种不同item的不回收，容易复用错误的item
        while (m_ItemViewChildren.Count > 0)
        {
            m_RecycleBin.AddScrapView(m_ItemViewChildren.Dequeue());
        }

        CalculateHeight();
        CalculateLayoutInfo();
    }

    /// <summary>
    /// 刷新index对应item的UI，item必须是可视的item，否则不进行处理
    /// </summary>
    /// <param name="index"></param>
    public void RefreshItem(int index)
    {
        if (index >= m_FirstPosition && index < m_FirstPosition + m_ItemViewChildren.Count)
        {
            m_Adapter.ProcessItemView(index, m_ItemViewChildren.GetElement(index - m_FirstPosition), this);
        }
    }

    /// <summary>
    /// 计算ScrollView的Content的高度
    /// </summary>
    protected void CalculateHeight()
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        var itemCount = m_Adapter.GetCount();
        var itemHeight = m_Adapter.GetItemSize().y;
        var totalRow = itemCount / column;
        totalRow += itemCount % column > 0 ? 1 : 0;
        var contentHeight = totalRow * itemHeight + (totalRow - 1) * spacing.y + padding.vertical;
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, contentHeight);
        m_ScrollView.content.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, m_ScrollView.viewport.rect.width);

        //计算当前在viewport下边缘处content的Y坐标
        var bottomY = m_ScrollView.content.anchoredPosition.y + m_ScrollView.viewport.rect.height;
        if (bottomY > contentHeight)
        {
            var oldPos = m_ScrollView.content.anchoredPosition;
            oldPos.y = Mathf.Max(0, contentHeight - m_ScrollView.viewport.rect.height);
            m_ScrollView.content.anchoredPosition = oldPos;
        }

        CheckAutoDrag();
        CheckArrowHint();
        CheckLoadMore(true);
    }

    /// <summary>
    /// 检查能否拖拽
    /// </summary>
    protected void CheckAutoDrag()
    {
        if (m_AutoDrag)
        {
            if (m_ScrollView != null)
                m_ScrollView.enabled = m_ScrollView.content.rect.height > m_ScrollView.viewport.rect.height;
        }
        else
        {
            if (m_ScrollView != null)
                m_ScrollView.enabled = true;
        }
    }

    /// <summary>
    /// 检查箭头指示
    /// </summary>
    protected void CheckArrowHint()
    {
        if (m_ScrollView == null)
            return;

        if (m_EnableArrowHintEvent)
        {
            //anchoredPosition.y停留在上方可能存在一些误差,不完全等于0，设置当大于等于0.13有向上箭头指示
            m_ArrowUpEvent.Invoke(m_ScrollView.content.anchoredPosition.y >= 0.13f);

            //计算当前在viewport下边缘处content的Y坐标
            var bottomY = m_ScrollView.content.anchoredPosition.y + m_ScrollView.viewport.rect.height;
            m_ArrowDownEvent.Invoke(m_ScrollView.content.rect.height > bottomY);
        }
    }

    /// <summary>
    /// 检查可锁定状态
    /// </summary>
    protected void CheckCanLockState()
    {
        if (m_ScrollView == null)
            return;

        if (m_EnableCanLockEvent)
        {
            if (m_ScrollView.content.rect.height > m_ScrollView.viewport.rect.height)
            {
                var contentY = m_ScrollView.content.anchoredPosition.y;
                if (m_IsLock && !m_ScrollView.dragging && contentY < 0)
                {
                    isLock = false;
                    return;
                }

                if (contentY > 0)
                {
                    isLock = true;
                }
            }
        }
    }

    /// <summary>
    /// 检查加载更多处理
    /// </summary>
    protected void CheckLoadMore(bool moveUp)
    {
        if (m_ScrollView == null)
            return;

        if (m_EnableLoadMoreEvent)
        {
            if (moveUp)
            {
                if (m_ScrollView.content.rect.height > m_ScrollView.viewport.rect.height)
                {
                    //计算当前在viewport下边缘处content的Y坐标
                    var bottomY = m_ScrollView.content.anchoredPosition.y + m_ScrollView.viewport.rect.height;
                    if (m_ScrollView.content.rect.height < bottomY)
                    {
                        if (Time.unscaledTime - m_CurrentTime < m_LoadMoreCD)
                        {
                            return;
                        }
                        m_CurrentTime = Time.unscaledTime;
                        m_LoadMoreEvent.Invoke();
                    }
                }
                else
                {
                    if (Time.unscaledTime - m_CurrentTime < m_LoadMoreCD)
                    {
                        return;
                    }
                    m_CurrentTime = Time.unscaledTime;
                    m_LoadMoreEvent.Invoke();
                }
            }
        }
    }

    /// <summary>
    /// 计算布局信息,回收不显示的item, 添加需要显示的item到可视列表中
    /// </summary>
    protected void CalculateLayoutInfo()
    {
        if (m_ScrollView == null)
            return;

        int firstShowItemIndex = 0;
        int showTotalItem = 0;
        GetVisibleItemRange(ref firstShowItemIndex, ref showTotalItem);

        var oldLastItemIndex = m_FirstPosition + m_ItemViewChildren.Count - 1;
        var newLastItemIndex = firstShowItemIndex + showTotalItem - 1;

        if (firstShowItemIndex != m_FirstPosition || newLastItemIndex != oldLastItemIndex)
        {
            //先清除需要隐藏的item
            if (firstShowItemIndex > m_FirstPosition)
            {
                for (int i = m_FirstPosition; i < firstShowItemIndex; ++i)
                {
                    if (m_ItemViewChildren.Count == 0)
                        break;

                    m_RecycleBin.AddScrapView(m_ItemViewChildren.DequeueFirst());
                }
            }

            if (oldLastItemIndex > newLastItemIndex)
            {
                for (int i = oldLastItemIndex; i > newLastItemIndex; --i)
                {
                    if (m_ItemViewChildren.Count == 0)
                        break;

                    m_RecycleBin.AddScrapView(m_ItemViewChildren.DequeueLast());
                }
            }

            var widthDelta = (m_ScrollView.content.rect.width + spacing.x - padding.horizontal) / column;
            var heightDelta = m_Adapter.GetItemSize().y + spacing.y;
            var itemWidth = widthDelta - spacing.x;

            //再复用item
            if (firstShowItemIndex < m_FirstPosition)
            {
                int startIndex = m_FirstPosition - 1;
                if (newLastItemIndex < m_FirstPosition)
                {
                    startIndex = newLastItemIndex;
                }

                for (int i = startIndex; i >= firstShowItemIndex; --i)
                {
                    var itemView = m_RecycleBin.GetScrapView();
                    if (itemView == null)
                    {
                        itemView = m_Adapter.GetItemView(m_ScrollView.content.gameObject);
                    }
                    SetItemViewVisible(itemView.rectTransform, 0);
                    itemView.extra = i;
                    m_Adapter.ProcessItemView(i, itemView, this);
                    m_ItemViewChildren.EnqueueFirst(itemView);
                    SetItemPosition(i, itemView.rectTransform, widthDelta, heightDelta, itemWidth);
                }
            }

            if (oldLastItemIndex < newLastItemIndex)
            {
                if (oldLastItemIndex < firstShowItemIndex)
                {
                    oldLastItemIndex = firstShowItemIndex - 1;
                }

                for (int i = oldLastItemIndex + 1; i <= newLastItemIndex; ++i)
                {
                    var itemView = m_RecycleBin.GetScrapView();
                    if (itemView == null)
                    {
                        itemView = m_Adapter.GetItemView(m_ScrollView.content.gameObject);
                    }
                    SetItemViewVisible(itemView.rectTransform, 0);
                    itemView.extra = i;
                    m_Adapter.ProcessItemView(i, itemView, this);
                    m_ItemViewChildren.EnqueueLast(itemView);
                    SetItemPosition(i, itemView.rectTransform, widthDelta, heightDelta, itemWidth);
                }
            }

            m_FirstPosition = firstShowItemIndex;
        }
    }

    /// <summary>
    /// 得到当前可视区域所显示的第一个Item在数据中的索引位置,以及能够显示的Item的总数目
    /// </summary>
    /// <param name="firstItemIndex">第一次可视item在数据中的索引</param>
    /// <param name="itemCount">可视的item数量</param>
    protected void GetVisibleItemRange(ref int firstItemIndex, ref int itemCount)
    {
        var contentTopLeft = m_ScrollView.content.anchoredPosition;
        var viewportHeight = m_ScrollView.viewport.rect.height;
        var deltaHeight = contentTopLeft.y - padding.top;
        var itemHeight = m_Adapter.GetItemSize().y + spacing.y;
        var curRowNumber = Mathf.FloorToInt(deltaHeight / itemHeight);

        firstItemIndex = curRowNumber * column;
        var deltaHeightToBottom = contentTopLeft.y + viewportHeight - padding.top;
        if (firstItemIndex < 0)
        {
            deltaHeightToBottom = viewportHeight - padding.top;
            firstItemIndex = 0;
            curRowNumber = 0;
        }

        var showHeight = deltaHeightToBottom - itemHeight * curRowNumber;
        var showTotalRow = Mathf.CeilToInt(showHeight / itemHeight);
        itemCount = showTotalRow * column;

        var dataCount = m_Adapter.GetCount();
        if (firstItemIndex + itemCount >= dataCount)
        {
            itemCount = dataCount - firstItemIndex;
        }
    }

    /// <summary>
    /// 定位到某一个item
    /// </summary>
    /// <param name="itemIndex">item在数据中的索引</param>
    /// <param name="resetStartPos">是否从content的开头开始动画滚动</param>
    /// <param name="useAnimation">是否使用动画滚动, false则为瞬间定位到item</param>
    /// <param name="factor">[0, 1] 0显示的viewport的最上面， 1显示的viewport的最下面， 0-1之间按比例显示在viewport中间</param>
    public void ScrollToItem(int itemIndex, bool resetStartPos = false, bool useAnimation = true, float factor = 0.5f)
    {
        if (m_ScrollView == null || m_Adapter == null)
            return;

        if (itemIndex < 0 || itemIndex >= m_Adapter.GetCount())
            return;

        var contentMinPos = 0;
        var contentMaxPos = m_ScrollView.content.rect.height - m_ScrollView.viewport.rect.height;
        if (contentMaxPos < 0)
            contentMaxPos = 0;

        var itemHeight = m_Adapter.GetItemSize().y;
        var rowIndex = itemIndex / column;
        ////////////////////targetPosY
        var targetHeight = padding.top + rowIndex * (itemHeight + spacing.y);

        var deltaHeight = m_ScrollView.viewport.rect.height - itemHeight;
        targetHeight -= Mathf.Clamp01(factor) * deltaHeight;
        targetHeight = Mathf.Clamp(targetHeight, contentMinPos, contentMaxPos);

        m_ScrollView.StartAnimation(new Vector2(0, targetHeight), resetStartPos, useAnimation);
    }

    #region 布局

    /// <summary>
    /// 设置item的显示位置
    /// </summary>
    protected void SetItemPosition(int itemIndex, RectTransform rectTransform, float widthDelta, float heightDelta, float itemWidth)
    {
        var columnIndex = itemIndex % column;
        if (m_ExpandWidth)
        {
            SetChildAlongAxis(rectTransform, 0, padding.left + columnIndex * widthDelta, itemWidth);
        }
        else
        {
            SetChildAlongAxis(rectTransform, 0, padding.left + columnIndex * widthDelta);
        }

        var rowIndex = itemIndex / column;
        SetChildAlongAxis(rectTransform, 1, padding.top + rowIndex * heightDelta);
    }

    protected void SetChildAlongAxis(RectTransform rect, int axis, float pos)
    {
        if (rect == null)
            return;
        m_Tracker.Add(this, rect, (DrivenTransformProperties)(3840 | (axis != 0 ? 4 : 2)));
        rect.SetInsetAndSizeFromParentEdge(axis != 0 ? RectTransform.Edge.Top : RectTransform.Edge.Left, pos, rect.sizeDelta[axis]);
    }

    protected void SetChildAlongAxis(RectTransform rect, int axis, float pos, float size)
    {
        if (rect == null)
            return;
        m_Tracker.Add(this, rect, (DrivenTransformProperties)(3840 | (axis != 0 ? 8196 : 4098)));
        rect.SetInsetAndSizeFromParentEdge(axis != 0 ? RectTransform.Edge.Top : RectTransform.Edge.Left, pos, size);
    }

    #endregion

    #region Item缓存管理

    /// <summary>
    /// 设置item的可视性,这里不直接使用GameObject的SetActive(), 因为UGUI的ui控件在OnEnable的时候会有GC产生
    /// 因此使用设置z深度改变item的可视性
    /// </summary>
    /// <param name="itemTransform"></param>
    /// <param name="z"></param>
    protected static void SetItemViewVisible(RectTransform itemTransform, float z = -100000)
    {
        var pos = itemTransform.localPosition;
        pos.z = z;
        itemTransform.localPosition = pos;
    }

    /// <summary>
    /// Item的缓存池
    /// </summary>
    protected class RecycleBin
    {
        private List<IItemView> m_CurrentScrap = new List<IItemView>();

        public void Clear()
        {
            int scrapCount = m_CurrentScrap.Count;
            for (int i = 0; i < scrapCount; i++)
            {
                Destroy(m_CurrentScrap[i].rectTransform.gameObject);
            }
            m_CurrentScrap.Clear();
        }

        public IItemView GetScrapView()
        {
            int size = m_CurrentScrap.Count;
            if (size > 0)
            {
                var item = m_CurrentScrap[size - 1];
                m_CurrentScrap.RemoveAt(size - 1);
                return item;
            }
            return null;
        }

        public void AddScrapView(IItemView scrap)
        {
            if (scrap == null)
                return;

            SetItemViewVisible(scrap.rectTransform);
            m_CurrentScrap.Add(scrap);
        }
    }

    #endregion
}