using UnityEngine;

public class NewBehaviourScript : MonoBehaviour
{
    public int layer;
    public Renderer r;

	void Start () {
 
	}
	
	// Update is called once per frame
	void Update () {

	    if (Input.GetKeyDown(KeyCode.A))
	    {
	        Debug.LogError(r.sortingOrder);
	        r.sortingOrder = layer;
	        Debug.LogError(r.sortingOrder);
        }
	}
}
