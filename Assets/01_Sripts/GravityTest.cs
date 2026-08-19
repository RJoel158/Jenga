using UnityEngine;

public class GravityTest : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        Rigidbody[] bodies = GetComponentsInChildren<Rigidbody>();

        foreach (Rigidbody rb in bodies)
        {
            rb.useGravity = false;
        }

        Debug.Log("Gravedad desactivada en " + bodies.Length + " bloques.");
    }
}
