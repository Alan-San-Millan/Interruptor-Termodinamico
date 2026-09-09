using UnityEngine;
using UnityEngine.Events;

// Arrastre en 3D de una pieza del modelo hasta su posición correcta.
// Se usa en la etapa de "armado tras corto circuito": el modelo queda desarmado
// (solo la carcasa) y el usuario debe arrastrar cada pieza a su lugar.
public class Piece3DDragAndDrop : MonoBehaviour
{
    [Tooltip("Transform que marca la posición/rotación correcta de esta pieza")]
    public Transform snapPosition;

    public float positionTolerance = 0.5f;
    public float rotationTolerance = 15f;

    [Tooltip("Se dispara una sola vez, cuando la pieza encaja correctamente")]
    public UnityEvent onPiezaColocada;

    private Camera mainCamera;
    private bool isDragging = false;
    private bool colocada = false;
    private float distanciaCamara;
    private Vector3 offsetArrastre;

    void Awake()
    {
        mainCamera = Camera.main;
    }

    void OnMouseDown()
    {
        if (colocada) return;

        isDragging = true;
        distanciaCamara = Vector3.Distance(mainCamera.transform.position, transform.position);

        Vector3 puntoEnPlano = PuntoEnPlanoDeArrastre(Input.mousePosition);
        offsetArrastre = transform.position - puntoEnPlano;
    }

    void OnMouseDrag()
    {
        if (!isDragging || colocada) return;

        Vector3 puntoEnPlano = PuntoEnPlanoDeArrastre(Input.mousePosition);
        transform.position = puntoEnPlano + offsetArrastre;
    }

    void OnMouseUp()
    {
        if (colocada) return;
        isDragging = false;

        bool posicionOk = Vector3.Distance(transform.position, snapPosition.position) <= positionTolerance;
        bool rotacionOk = Quaternion.Angle(transform.rotation, snapPosition.rotation) <= rotationTolerance;

        if (posicionOk && rotacionOk)
        {
            transform.position = snapPosition.position;
            transform.rotation = snapPosition.rotation;
            colocada = true;

            Debug.Log($"{name}: pieza colocada correctamente.");
            onPiezaColocada?.Invoke();

            // Ya no se puede volver a mover una vez colocada
            this.enabled = false;
        }
    }

    // Proyecta el mouse sobre un plano perpendicular a la cámara, a la distancia
    // en que estaba la pieza al empezar el arrastre. Evita depender de colliders
    // de fondo (que pueden no existir una vez desarmado el modelo).
    private Vector3 PuntoEnPlanoDeArrastre(Vector3 mousePos)
    {
        Ray ray = mainCamera.ScreenPointToRay(mousePos);
        Plane plano = new Plane(-mainCamera.transform.forward, mainCamera.transform.position + mainCamera.transform.forward * distanciaCamara);

        if (plano.Raycast(ray, out float distanciaHit))
        {
            return ray.GetPoint(distanciaHit);
        }

        return transform.position;
    }

    public void ReiniciarPieza(Vector3 posicionInicial, Quaternion rotacionInicial)
    {
        colocada = false;
        isDragging = false;
        this.enabled = true;
        transform.position = posicionInicial;
        transform.rotation = rotacionInicial;
    }
}
