using UnityEngine;

public class HomingTarget : MonoBehaviour
{
    private void OnHomingHit()
    {
        gameObject.SetActive(false);
    }
}
