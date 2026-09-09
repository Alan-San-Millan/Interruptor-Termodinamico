using UnityEngine;

public class PieceDragAndDrop : MonoBehaviour
{
    private Vector3 initialPosition;
    private Quaternion initialRotation;
    private bool isDragging = false;
    private Camera mainCamera;
    
    // El "marker" o posición objetivo de la pieza
    public Transform snapPosition;
    
    // Tolerancia para la posición y rotación (ajustable en el Inspector)
    public float positionTolerance = 0.5f;
    public float rotationTolerance = 5.0f;

    void Awake()
    {
        initialPosition = transform.position;
        initialRotation = transform.rotation;
        mainCamera = Camera.main;
    }

    void OnMouseDown()
    {
        // Comienza el arrastre
        isDragging = true;
    }

    void OnMouseDrag()
    {
        if (isDragging)
        {
            // Mueve la pieza para que siga la posición del ratón en el mundo 3D
            Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;
            if (Physics.Raycast(ray, out hit, 100f))
            {
                transform.position = hit.point;
            }
        }
    }

    void OnMouseUp()
    {
        isDragging = false;

        // Comprobamos si la pieza está cerca de la posición correcta
        if (Vector3.Distance(transform.position, snapPosition.position) <= positionTolerance &&
            Quaternion.Angle(transform.rotation, snapPosition.rotation) <= rotationTolerance)
        {
            // ¡Correcto! La pieza encaja
            transform.position = snapPosition.position;
            transform.rotation = snapPosition.rotation;
            
            // Aquí puedes agregar la lógica para el contador de puntos
            // Si usas el GameManager, sería gameManager.AddScore();
            
            Debug.Log("¡Pieza colocada correctamente!");
            // Puedes desactivar este script o el collider para que la pieza no se pueda mover más
            this.enabled = false;
        }
        else
        {
            // Si es incorrecto, la pieza vuelve a su lugar de origen
            Debug.Log("Incorrecto. Vuelve a intentarlo.");
            transform.position = initialPosition;
            transform.rotation = initialRotation;
        }
    }
}