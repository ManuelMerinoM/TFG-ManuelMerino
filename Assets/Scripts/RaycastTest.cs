using System.Collections;
using System.Collections.Generic;
using UnityEngine;



public class RaycastTest : MonoBehaviour
{
    public LayerMask trackLayer;
    
    void OnDrawGizmos()
    {
        Vector3 start = transform.position + Vector3.up * 10;
        RaycastHit hit;
        
        if (Physics.Raycast(start, Vector3.down, out hit, 20, trackLayer))
        {
            Gizmos.color = Color.green;
            Gizmos.DrawLine(start, hit.point);
            Gizmos.DrawSphere(hit.point, 0.5f);
            
            // Muestra el nombre del objeto golpeado en la consola
            Debug.Log("Raycast hit: " + hit.collider.gameObject.name);
        }
        else
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(start, start + Vector3.down * 20);
            Debug.Log("Raycast no hit");
        }
    }
}
