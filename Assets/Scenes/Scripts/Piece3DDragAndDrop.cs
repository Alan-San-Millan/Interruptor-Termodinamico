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

    [Tooltip("Margen de error al soltar, como fracción del alto de la pantalla. " +
             "0.08 = encaja si se suelta a menos de un 8% de la pantalla del destino.")]
    [Range(0.01f, 0.3f)]
    public float margenDeError = 0.08f;

    [Tooltip("Se dispara cuando la pieza encaja correctamente")]
    public UnityEvent onPiezaColocada;

    [Tooltip("Se dispara cuando se suelta la pieza lejos de su destino")]
    public UnityEvent onPiezaFallada;

    private bool colocada = false;
    private float profundidadCamara;
    private Vector3 desfaseCentroVisual;
    private Vector3 posicionDispersa;
    private Quaternion rotacionDispersa;

    public void OnPointerDown(PointerEventData eventData)
    {
        if (colocada) return;

        Camera cam = CamaraDe(eventData);
        if (cam == null) return;

        // El pivote del mesh no suele coincidir con su centro visual: guardamos
        // la diferencia para que sea el centro de la pieza el que sigue al
        // dedo/cursor, y no una esquina.
        Renderer rend = GetComponentInChildren<Renderer>();
        Vector3 centroVisual = rend != null ? rend.bounds.center : transform.position;
        desfaseCentroVisual = transform.position - centroVisual;

        // Profundidad = proyección sobre el eje de vista, NO la distancia
        // euclídea: con la pieza en un borde de la pantalla esa distancia es
        // mayor que la profundidad real y la pieza saltaría hacia atrás.
        profundidadCamara = Vector3.Dot(centroVisual - cam.transform.position, cam.transform.forward);
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        // La distancia a cámara ya se fijó en OnPointerDown.
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (colocada) return;

        Camera cam = CamaraDe(eventData);
        if (cam == null) return;

        // La pieza va pegada al cursor/dedo, centrada en él.
        transform.position = PuntoEnPlanoDeArrastre(eventData, cam) + desfaseCentroVisual;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (colocada) return;

        Camera cam = CamaraDe(eventData);
        if (cam == null) return;

        // El margen se mide en pantalla, no en el mundo: es lo que ve el
        // jugador y no depende de la profundidad ni de la escala del modelo.
        Vector2 enPantalla = cam.WorldToScreenPoint(transform.position);
        Vector2 destinoEnPantalla = cam.WorldToScreenPoint(snapPosition.position);

        if (Vector2.Distance(enPantalla, destinoEnPantalla) <= Screen.height * margenDeError)
        {
            transform.position = snapPosition.position;
            transform.rotation = snapPosition.rotation;
            colocada = true;
            this.enabled = false;

            Debug.Log($"{name}: pieza colocada correctamente.");
            onPiezaColocada?.Invoke();
        }
        else
        {
            // Como en el juego de arrastrar nombres: vuelve a su lugar de origen
            transform.position = posicionDispersa;
            transform.rotation = rotacionDispersa;

            Debug.Log($"{name}: posición incorrecta, vuelve al inicio.");
            onPiezaFallada?.Invoke();
        }
    }

    private Camera CamaraDe(PointerEventData eventData)
    {
        return eventData.pressEventCamera != null ? eventData.pressEventCamera : Camera.main;
    }

    // Proyecta el puntero sobre un plano perpendicular a la cámara, a la
    // profundidad que tenía la pieza al empezar el arrastre.
    private Vector3 PuntoEnPlanoDeArrastre(PointerEventData eventData, Camera cam)
    {
        Ray ray = cam.ScreenPointToRay(eventData.position);
        Plane plano = new Plane(-cam.transform.forward, cam.transform.position + cam.transform.forward * profundidadCamara);

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
        posicionDispersa = posicionInicial;
        rotacionDispersa = rotacionInicial;
        transform.position = posicionInicial;
        transform.rotation = rotacionInicial;
    }
}
