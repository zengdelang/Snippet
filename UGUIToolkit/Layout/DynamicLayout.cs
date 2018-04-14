using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;

public abstract class DynamicLayout : MonoBehaviour
{
    [Serializable]
    public class BoolUnityEvent : UnityEvent<bool>
    {

    }

    protected class LayoutParam
    {
        
    }

    protected class LayoutInfo
    {
        public IItemView   itemView;
        public LayoutParam layoutParam;
    }

    [SerializeField] protected RectOffset m_Padding = new RectOffset();
    [SerializeField] protected Vector2 m_Spacing = Vector2.zero;            //布局信息,Item和Item之间的水平和竖直间隔
    [SerializeField] protected ScrollView m_ScrollView;                     //布局监听ScrollView的onContentPosChanged来动态布局

    protected DrivenRectTransformTracker m_Tracker;
    protected RecycleBin m_RecycleBin = new RecycleBin();                   //ItemView的缓存池
    protected Deque<LayoutInfo> m_ItemViewChildren = new Deque<LayoutInfo>(); //保存可视的itemView的双端队列
    protected IFixedSizeItemAdapter m_Adapter; //布局所使用的适配器

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

    protected virtual void OnEnable()
    {
        SetupPivotAndAnchor();
        RefreshCurrentItem();
    }

    protected virtual void OnDisable()
    {
        m_Tracker.Clear();

        if (m_Adapter == null)
            return;
        //在组件被禁用或者Destroy的时候会调用OnDisable(),此时把所有的item进行回收
        while (m_ItemViewChildren.Count > 0)
        {
            m_RecycleBin.AddLayoutInfo(m_ItemViewChildren.Dequeue());
        }

        for (int i = 0, count = m_RecycleBin.Count; i < count; ++i)
        {
            m_Adapter.RecycleItemView(m_RecycleBin.PeekLayoutInfo(i).itemView);
        }
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
    /// 刷新所有item，布局第一个item开始重新布局
    /// </summary>
    public abstract void RefreshAllItem();

    /// <summary>
    /// 刷新当前所有可见item,通常用于item对应数据被修改，或者有数据被删除时通知刷新UI
    /// </summary>
    public abstract void RefreshCurrentItem();

    /// <summary>
    /// 刷新index对应item的UI，当item是可视的时候才会刷新对应UI，否则不做任何处理
    /// </summary>
    /// <param name="index"></param>
    public abstract void RefreshItem(int index);

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
    /// 设置item的可视性,这里不直接使用GameObject的SetActive(), 因为UGUI的ui控件在OnEnable的时候会有GC产生
    /// 因此使用设置z深度改变item的可视性
    /// </summary>
    /// <param name="itemTransform"></param>
    /// <param name="isVisible">是否可视</param>
    protected static void SetItemVisible(RectTransform itemTransform, bool isVisible)
    {
        var pos = itemTransform.localPosition;
        pos.z = isVisible ? 0 : -100000;
        itemTransform.localPosition = pos;
    }

    #region 布局

    /// <summary>
    /// 监听ScrollView的Content位置更改事件, 根据位置重新计算布局
    /// </summary>
    /// <param name="oldPos"></param>
    /// <param name="newPos"></param>
    public abstract void OnContentPositionChanged(Vector2 oldPos, Vector2 newPos);

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
    /// Item的缓存池
    /// </summary>
    protected class RecycleBin
    {
        private List<LayoutInfo> m_LayoutInfoList = new List<LayoutInfo>();

        public int Count
        {
            get { return m_LayoutInfoList.Count; }
        }

        public void Clear()
        {
            int scrapCount = m_LayoutInfoList.Count;
            for (int i = 0; i < scrapCount; i++)
            {
                Destroy(m_LayoutInfoList[i].itemView.rectTransform.gameObject);
            }
            m_LayoutInfoList.Clear();
        }

        public LayoutInfo PeekLayoutInfo(int index)
        {
            if (index < 0 || index >= Count)
                return null;

            return m_LayoutInfoList[index];
        }

        public LayoutInfo GetLayoutInfo()
        {
            int size = m_LayoutInfoList.Count;
            if (size > 0)
            {
                var item = m_LayoutInfoList[size - 1];
                m_LayoutInfoList.RemoveAt(size - 1);
                return item;
            }
            return null;
        }

        public void AddLayoutInfo(LayoutInfo layoutInfo)
        {
            if (layoutInfo == null)
                return;

            SetItemVisible(layoutInfo.itemView.rectTransform, false);
            m_LayoutInfoList.Add(layoutInfo);
        }
    }

    #endregion
}
