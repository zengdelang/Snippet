using System;
namespace JsonFx.U3DEditor
{
	/** Specifies that members of this class that should be serialized must be explicitly specified.
	 * Classes that this attribute is applied to need to explicitly
	 * declare every member that should be serialized with the JsonMemberAttribute.
	 * \see JsonMemberAttribute
	 */
	public class JsonOptInAttribute : Attribute
	{
		public JsonOptInAttribute ()
		{
			
		}
	}

	public class JsonUseTypeHintAttribute : Attribute
	{
		public JsonUseTypeHintAttribute () {}
	}

    /// <summary>
    /// ���л���ʱ������Ϣд�룬�����е�ʱ��ֱ�����ɶ�Ӧ��Ķ���
    /// </summary>
    public class JsonClassTypeAttribute : Attribute
    {
        public JsonClassTypeAttribute() { }
    }
}

