using System.Collections.Generic;
using TMPro;
using UnityEngine;

// Etapa de "desarmado del interruptor": al empezar, la carcasa queda vacía y
// las piezas se reparten al azar a los costados de la pantalla, con un punto
// verde marcando cada destino. El usuario debe arrastrar cada pieza a su lugar.
public class DisassemblyGame : MonoBehaviour
{
    [Header("Piezas del desarme")]
    [Tooltip("Piezas que el jugador tiene que volver a colocar. Cada una lleva su propio Snap Position.")]
    public Piece3DDragAndDrop[] piezas;

    [Header("Zonas donde caen las piezas (coordenadas de pantalla)")]
    [Tooltip("Franja izquierda: x mínimo y máximo, siendo 0 el borde izquierdo y 1 el derecho")]
    public Vector2 franjaIzquierda = new Vector2(0.04f, 0.26f);

    [Tooltip("Franja derecha: x mínimo y máximo")]
    public Vector2 franjaDerecha = new Vector2(0.66f, 0.96f);

    [Tooltip("Alto utilizable: y mínimo y máximo, siendo 0 abajo y 1 arriba")]
    public Vector2 franjaVertical = new Vector2(0.10f, 0.80f);

    [Header("Se ocultan mientras se juega")]
    [Tooltip("El resto del mecanismo dentro de la carcasa. Cada objeto vuelve al " +
             "estado en el que estaba cuando se vuelve a la pantalla de selección.")]
    public GameObject[] objetosAOcultar;

    [Tooltip("Paneles con los botones de elegir etapa. Se ocultan al jugar y se " +
             "vuelven a mostrar siempre al terminar.")]
    public GameObject[] panelesSeleccion;

    [Header("Puntaje")]
    [Tooltip("Texto donde se muestra el puntaje de esta etapa")]
    public TextMeshProUGUI textoPuntaje;

    [Tooltip("Puntos que suma colocar una pieza en su lugar")]
    public int puntosPorAcierto = 1;

    [Tooltip("Puntos que resta soltar una pieza en un lugar equivocado")]
    public int puntosPorError = 1;

    [Header("Paneles")]
    [Tooltip("Panel que se muestra al colocar todas las piezas correctamente")]
    public GameObject panelCompletado;

    [Tooltip("Botón que aparece al terminar y devuelve a la pantalla de selección")]
    public GameObject botonContinuar;

    [Header("Punto verde de destino")]
    [Tooltip("Material del punto que marca cada posición correcta, sin indicar qué pieza va ahí")]
    public Material materialIndicador;

    [Tooltip("Diámetro del punto, en unidades del mundo")]
    public float tamanoIndicador = 0.25f;

    private GameObject[] indicadores;
    private bool[] estabanVisibles;
    private int piezasColocadas = 0;
    private int puntaje = 0;
    private bool juegoIniciado = false;

    void Awake()
    {
        indicadores = new GameObject[piezas.Length];

        // El marcador y el botón de continuar recién aparecen cuando toca
        if (textoPuntaje != null) textoPuntaje.gameObject.SetActive(false);
        if (botonContinuar != null) botonContinuar.SetActive(false);

        // Guardamos si cada objeto estaba visible de entrada: varios (como los
        // marcadores _Encendido/_Apagado) vienen apagados a propósito y no hay
        // que encenderlos al restaurar.
        estabanVisibles = new bool[objetosAOcultar.Length];
        for (int i = 0; i < objetosAOcultar.Length; i++)
        {
            estabanVisibles[i] = objetosAOcultar[i] != null && objetosAOcultar[i].activeSelf;
        }

        CargarDestinosCruzados();

        if (materialIndicador == null)
        {
            Debug.LogWarning($"{name}: no hay material asignado en 'Material Indicador', no se van a ver los puntos verdes.");
        }

        for (int i = 0; i < piezas.Length; i++)
        {
            Piece3DDragAndDrop pieza = piezas[i];
            if (pieza == null || pieza.snapPosition == null)
            {
                Debug.LogWarning($"{name}: el elemento {i} de 'Piezas' está sin asignar o no tiene Snap Position.");
                continue;
            }

            // Nos suscribimos por código para no depender de que cada pieza
            // tenga cableado su UnityEvent manualmente en el Inspector.
            int indice = i;
            pieza.onPiezaColocada.AddListener(() => RegistrarPiezaColocada(indice));
            pieza.onPiezaFallada.AddListener(RegistrarError);

            // Por defecto el modelo se ve armado: cada pieza arranca en su
            // posición correcta (Snap) y sin poder arrastrarse todavía.
            pieza.transform.position = pieza.snapPosition.position;
            pieza.transform.rotation = pieza.snapPosition.rotation;
            pieza.enabled = false;

            indicadores[i] = CrearIndicador(pieza);
        }
    }

    // Punto verde en el centro exacto del destino de la pieza. No dice qué
    // pieza va ahí: solo marca que ese hueco espera una.
    private GameObject CrearIndicador(Piece3DDragAndDrop pieza)
    {
        if (materialIndicador == null) return null;

        GameObject punto = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        punto.name = "Punto_" + pieza.name;

        // Cuelga del manager, NO del Snap: los Snap son copias con malla de las
        // piezas y se ocultan durante el juego, así que se llevarían el punto.
        punto.transform.SetParent(transform, worldPositionStays: false);
        punto.transform.position = pieza.snapPosition.position;
        punto.transform.rotation = Quaternion.identity;

        // Compensamos la escala heredada para que el punto mida siempre lo
        // mismo en unidades del mundo, sin importar la escala del padre.
        Vector3 escalaPadre = transform.lossyScale;
        punto.transform.localScale = new Vector3(
            tamanoIndicador / Mathf.Max(Mathf.Abs(escalaPadre.x), 0.0001f),
            tamanoIndicador / Mathf.Max(Mathf.Abs(escalaPadre.y), 0.0001f),
            tamanoIndicador / Mathf.Max(Mathf.Abs(escalaPadre.z), 0.0001f)
        );

        // Sin collider, para que no bloquee el arrastre de las piezas.
        Collider col = punto.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = punto.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = materialIndicador;

        punto.SetActive(false);
        return punto;
    }

    public void IniciarJuego()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError($"{name}: no hay cámara con el tag 'MainCamera', no puedo repartir las piezas.");
            return;
        }

        if (panelCompletado != null) panelCompletado.SetActive(false);

        // Vaciamos la carcasa y sacamos los botones de la pantalla
        MostrarObjetosOcultables(false);
        MostrarPanelesSeleccion(false);

        piezasColocadas = 0;
        puntaje = 0;
        juegoIniciado = true;

        if (textoPuntaje != null) textoPuntaje.gameObject.SetActive(true);
        ActualizarTextoPuntaje();

        // El reparto se sortea en cada partida, así que las piezas nunca caen
        // dos veces en el mismo lugar.
        List<Rect> celdas = RepartirCeldas(piezas.Length);

        int puntosVisibles = 0;
        for (int i = 0; i < piezas.Length; i++)
        {
            Piece3DDragAndDrop pieza = piezas[i];
            if (pieza == null || pieza.snapPosition == null) continue;

            pieza.ReiniciarPieza(PosicionAleatoriaEn(celdas[i], pieza.snapPosition.position, cam),
                                 pieza.snapPosition.rotation);

            if (indicadores[i] != null)
            {
                indicadores[i].SetActive(true);
                puntosVisibles++;
            }
        }

        Debug.Log($"{name}: desarme iniciado con {piezas.Length} piezas y {puntosVisibles} puntos de destino.");
    }

    // Reparte la pantalla en celdas (mitad a cada lado del interruptor) y las
    // baraja, para que cada pieza caiga en un lugar distinto en cada partida
    // sin que dos piezas se amontonen.
    private List<Rect> RepartirCeldas(int cantidad)
    {
        var celdas = new List<Rect>();
        int enIzquierda = Mathf.CeilToInt(cantidad / 2f);

        AgregarCeldasDeLado(celdas, enIzquierda, franjaIzquierda);
        AgregarCeldasDeLado(celdas, cantidad - enIzquierda, franjaDerecha);

        for (int i = celdas.Count - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            (celdas[i], celdas[j]) = (celdas[j], celdas[i]);
        }

        return celdas;
    }

    private void AgregarCeldasDeLado(List<Rect> celdas, int cantidad, Vector2 franjaX)
    {
        if (cantidad <= 0) return;

        int columnas = Mathf.Min(2, cantidad);
        int filas = Mathf.CeilToInt(cantidad / (float)columnas);
        float ancho = (franjaX.y - franjaX.x) / columnas;
        float alto = (franjaVertical.y - franjaVertical.x) / filas;

        for (int i = 0; i < cantidad; i++)
        {
            celdas.Add(new Rect(
                franjaX.x + (i % columnas) * ancho,
                franjaVertical.x + (i / columnas) * alto,
                ancho, alto));
        }
    }

    private Vector3 PosicionAleatoriaEn(Rect celda, Vector3 posicionDestino, Camera cam)
    {
        // Un margen dentro de la celda evita que dos piezas vecinas se toquen
        float margenX = celda.width * 0.2f;
        float margenY = celda.height * 0.2f;

        float x = Random.Range(celda.xMin + margenX, celda.xMax - margenX);
        float y = Random.Range(celda.yMin + margenY, celda.yMax - margenY);

        // Conserva la profundidad de su destino: la pieza se ve del mismo
        // tamaño que cuando está colocada.
        float profundidad = cam.WorldToViewportPoint(posicionDestino).z;
        return cam.ViewportToWorldPoint(new Vector3(x, y, profundidad));
    }

    // Cada pieza necesita conocer los destinos del resto para distinguir un
    // intento fallido de un simple "la agarré sin querer y la solté".
    private void CargarDestinosCruzados()
    {
        var destinos = new List<Transform>();
        foreach (Piece3DDragAndDrop pieza in piezas)
        {
            if (pieza != null && pieza.snapPosition != null) destinos.Add(pieza.snapPosition);
        }

        Transform[] todos = destinos.ToArray();
        foreach (Piece3DDragAndDrop pieza in piezas)
        {
            if (pieza != null) pieza.otrosDestinos = todos;
        }
    }

    private void MostrarObjetosOcultables(bool visibles)
    {
        for (int i = 0; i < objetosAOcultar.Length; i++)
        {
            if (objetosAOcultar[i] == null) continue;

            // Al restaurar respetamos el estado original: los que ya venían
            // apagados (marcadores de animación) siguen apagados.
            objetosAOcultar[i].SetActive(visibles && estabanVisibles[i]);
        }
    }

    private void MostrarPanelesSeleccion(bool visibles)
    {
        // Estos siempre se muestran al volver, sin mirar su estado inicial: en
        // la escena arrancan apagados porque la primera etapa es otra.
        for (int i = 0; i < panelesSeleccion.Length; i++)
        {
            if (panelesSeleccion[i] != null) panelesSeleccion[i].SetActive(visibles);
        }
    }

    private void RegistrarPiezaColocada(int indice)
    {
        if (!juegoIniciado) return;

        piezasColocadas++;
        puntaje += puntosPorAcierto;
        ActualizarTextoPuntaje();

        if (indicadores[indice] != null) indicadores[indice].SetActive(false);

        Debug.Log($"{name}: pieza colocada ({piezasColocadas}/{piezas.Length}). Puntaje: {puntaje}");

        if (piezasColocadas >= piezas.Length)
        {
            Completar();
        }
    }

    private void RegistrarError()
    {
        if (!juegoIniciado) return;

        puntaje -= puntosPorError;
        ActualizarTextoPuntaje();
    }

    private void ActualizarTextoPuntaje()
    {
        if (textoPuntaje != null)
        {
            textoPuntaje.text = $"Puntuación: {puntaje}";
        }
    }

    private void Completar()
    {
        juegoIniciado = false;

        Debug.Log($"{name}: ¡modelo reensamblado correctamente! Puntaje final: {puntaje}");

        if (panelCompletado != null) panelCompletado.SetActive(true);

        // No volvemos solos a la pantalla de selección: el jugador decide
        // cuándo, con el botón que aparece al costado.
        if (botonContinuar != null) botonContinuar.SetActive(true);
    }

    // Lo llama el botón que aparece al terminar el juego.
    public void VolverASeleccion()
    {
        if (botonContinuar != null) botonContinuar.SetActive(false);
        if (panelCompletado != null) panelCompletado.SetActive(false);
        if (textoPuntaje != null) textoPuntaje.gameObject.SetActive(false);

        // El interruptor vuelve a estar completo y el jugador queda otra vez
        // en la pantalla donde elige entre el juego y la animación.
        MostrarObjetosOcultables(true);
        MostrarPanelesSeleccion(true);
    }
}
