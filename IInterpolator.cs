public interface IInterpolator
{
    /**
     * 将动画已经消耗的时间的分数映射到一个表示插值的分数。
     * 然后将插值与动画的变化值相乘来推导出当前已经过去的动画时间的动画变化量。
     *
     * @param input  一个0到1.0表示动画当前点的值，0表示开头。1表示结尾
     * @return   插值。它的值可以大于1来超出目标值，也小于0来空破底线。
     */
    float GetInterpolation(float input);
}
