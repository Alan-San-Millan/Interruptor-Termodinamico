using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;

// Arrastre en 3D de una pieza del modelo hasta su posición correcta.
// Usa el sistema de eventos (EventSystem + PhysicsRaycaster en la cámara) en
// lugar de OnMouseDown/OnMouseDrag, porque el proyecto está configurado con el
// Input System nuevo, donde esos mensajes nunca se disparan.
public class Piece3DDragAndDrop : MonoBehaviour, IPointerDownHandler, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    [Tooltip("Transform que marca la posición/rotación correcta de esta pieza")]
    public Transform snapPosition;

    [Tooltip("Qué tan cerca del destino hay que soltarla para que encaje")]
    public float positionTolerance = 0.5f;

    [Tooltip("Se dispara una sola vez, cuando la pieza encaja correctamente")]
    public UnityEvent onPiezaColocada;

    private bool colocada = false;
    private float distanciaCamara;
    private Vector3 offsetArrastre;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (colocada) return;

        Camera cam = CamaraDe(eventData);
        if (cam == null) return;

        distanciaCamara = Vector3.Distance(cam.transform.position, transform.position);
        offsetArrastre = transform.position - PuntoEnPlanoDeArrastre(eventData, cam);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // El offset ya se calculó en OnPointerDown; acá no hace falta nada más.
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (colocada) return;

        Camera cam = CamaraDe(eventData);
        if (cam == null) return;

        transform.position = PuntoEnPlanoDeArrastre(eventData, cam) + offsetArrastre;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (colocada) return;

        if (Vector3.Distance(transform.position, snapPosition.position) <= positionTolerance)
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

    private Camera CamaraDe(PointerEventData eventData)
    {
        return eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
    }

    // Proyecta el puntero sobre un plano perpendicular a la cámara, a la
    // distancia en que estaba la pieza al empezar el arrastre.
    private Vector3 PuntoEnPlanoDeArrastre(PointerEventData eventData, Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(eventData.position);
        Plane plano = new Plane(-cam.transform.forward, cam.transform.position + cam.transform.forward * distanciaCamara);

        if (plano.Raycast(ray, out float distanciaHit))
        {
            return ray.GetPoint(distanciaHit);
        }

        return transform.position;
    }

    public void ReiniciarPieza(Vector3 posicionInicial, Quaternion rotacionInicial)
    {
        colocada = false;
        this.enabled = true;
        gameObject.SetActive(true);
        transform.position = posicionInicial;
        transform.rotation = rotacionInicial;
    }
}
