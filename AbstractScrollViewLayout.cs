using UnityEngine;
using UnityEngine.UI;

public abstract class AbstractScrollViewLayout : LayoutGroup
{
    public abstract void OnContentPositionChanged(ScrollView scrollView, Bounds viewBounds, Bounds contentBounds);
}
