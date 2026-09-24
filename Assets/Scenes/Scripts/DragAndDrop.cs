using UnityEngine;
using UnityEngine.EventSystems;

public class DragAndDrop : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
{
    private RectTransform rectTransform;
    private CanvasGroup canvasGroup;
    private Vector2 initialPosition;
    private Canvas parentCanvas;

    [Header("Configuración de Destino (UI)")]
    [Tooltip("Arrastra aquí el cuadro blanco vacío que corresponde a esta pieza")]
    public RectTransform cuadroDestino;

    [Tooltip("Sensibilidad: qué tan cerca debe estar para que se pegue automáticamente")]
    public float distanciaAceptable = 50f;

    private GameManager gameManager;
    private bool yaColocada = false;

    // Destinos del resto de las etiquetas, para distinguir "la solté sobre el
    // hueco de otra pieza" (error real) de "la moví y la solté en cualquier
    // lado" (no debería costar puntos).
    private RectTransform[] otrosDestinos;

    void Awake()
    {
        rectTransform = GetComponent<RectTransform>();
        canvasGroup = GetComponent<CanvasGroup>();
        initialPosition = rectTransform.anchoredPosition; // Guardamos posición original
        parentCanvas = GetComponentInParent<Canvas>();

        // Buscamos el GameManager en la escena
        gameManager = Object.FindAnyObjectByType<GameManager>();

        DragAndDrop[] todasLasEtiquetas = Object.FindObjectsByType<DragAndDrop>(FindObjectsSortMode.None);
        var destinos = new System.Collections.Generic.List<RectTransform>();
        foreach (DragAndDrop etiqueta in todasLasEtiquetas)
        {
            if (etiqueta != this && etiqueta.cuadroDestino != null)
            {
                destinos.Add(etiqueta.cuadroDestino);
            }
        }
        otrosDestinos = destinos.ToArray();
    }

    // Devuelve la etiqueta a su lugar de origen para poder volver a jugar
    public void ReiniciarEtiqueta()
    {
        yaColocada = false;
        this.enabled = true;
        rectTransform.anchoredPosition = initialPosition;

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 1f;
            canvasGroup.blocksRaycasts = true;
        }
    }

    public void OnBeginDrag(PointerEventData eventData)
    {
        if (yaColocada) return; // Si ya se colocó correctamente, no permitir moverla

        canvasGroup.alpha = 0.6f;
        canvasGroup.blocksRaycasts = false;
        transform.SetAsLastSibling(); // Poner al frente al arrastrar
    }

    public void OnDrag(PointerEventData eventData)
    {
        if (yaColocada) return;

        // Movimiento adaptado a la escala del Canvas[cite: 3]
        rectTransform.anchoredPosition += eventData.delta / parentCanvas.scaleFactor;
    }

    public void OnEndDrag(PointerEventData eventData)
    {
        if (yaColocada) return;

        canvasGroup.alpha = 1f;
        canvasGroup.blocksRaycasts = true;

        // Si no asignaste un destino en el Inspector, la pieza regresará siempre
        if (cuadroDestino != null)
        {
            // Calculamos la distancia entre el centro de la pieza y el centro del cuadro destino
            float distancia = Vector3.Distance(rectTransform.position, cuadroDestino.position);

            if (distancia < distanciaAceptable)
            {
                Debug.Log("¡Correcto! Acoplando pieza.");
                
                // Efecto imán: Colocamos la pieza exactamente sobre el cuadro blanco[cite: 3]
                rectTransform.position = cuadroDestino.position;
                yaColocada = true;

                // Sumar punto en el GameManager[cite: 4]
                if (gameManager != null)
                {
                    gameManager.AddScore();
                }

                // Desactivamos el script para que no se pueda mover más
                this.enabled = false;
                return;
            }
        }
        
        // Solo se penaliza si la soltó sobre el hueco de OTRA etiqueta: si la
        // soltó en cualquier otro lado puede haberla movido sin querer.
        if (EstaSobreOtroDestino())
        {
            Debug.Log("Incorrecto: ahí va otra etiqueta. Se pierde un punto.");
            if (gameManager != null)
            {
                gameManager.PerderPunto();
            }
        }
        else
        {
            Debug.Log("La soltó sin intentar colocarla, vuelve al inicio sin penalización.");
        }

        rectTransform.anchoredPosition = initialPosition;
    }

    private bool EstaSobreOtroDestino()
    {
        if (otrosDestinos == null) return false;

        foreach (RectTransform destino in otrosDestinos)
        {
            if (destino == null) continue;

            if (Vector3.Distance(rectTransform.position, destino.position) < distanciaAceptable)
            {
                return true;
            }
        }

        return false;
    }
}