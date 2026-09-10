using UnityEngine;
using UnityEngine.Events;

// Etapa de "desarmado del interruptor": las piezas parten armadas (cada una
// en su posición correcta) y, al presionar el botón, saltan a una posición
// dispersa (misma orientación y profundidad, solo corridas en X/Y). El
// usuario debe arrastrar cada una de vuelta a su lugar.
public class DisassemblyGame : MonoBehaviour
{
    [System.Serializable]
    public class PiezaDesarme
    {
        [Tooltip("La pieza del modelo que hay que volver a colocar")]
        public Piece3DDragAndDrop pieza;

        [Tooltip("Posición/rotación dispersa donde aparece la pieza al desarmar")]
        public Transform posDesarmada;
    }

    [Header("Piezas del desarme")]
    public PiezaDesarme[] piezas;

    [Header("Paneles")]
    [Tooltip("Panel que se muestra mientras se juega esta etapa (opcional)")]
    public GameObject panelJuego;

    [Tooltip("Panel que se muestra al colocar todas las piezas correctamente")]
    public GameObject panelCompletado;

    [Header("Indicador visual de destino (opcional)")]
    [Tooltip("Material semi-transparente para marcar cada posición correcta, sin indicar qué pieza va ahí")]
    public Material materialIndicador;

    [Tooltip("Tamaño del cubo indicador")]
    public float tamanoIndicador = 0.25f;

    private GameObject[] indicadores;
    private int piezasColocadas = 0;
    private bool juegoIniciado = false;

    void Awake()
    {
        indicadores = new GameObject[piezas.Length];

        for (int i = 0; i < piezas.Length; i++)
        {
            PiezaDesarme p = piezas[i];
            if (p.pieza == null || p.posDesarmada == null || p.pieza.snapPosition == null)
            {
                Debug.LogWarning($"{name}: el elemento {i} de 'Piezas' tiene una referencia sin asignar (Pieza/Pos Desarmada/Snap Position).");
                continue;
            }

            // Nos suscribimos por código para no depender de que cada pieza
            // tenga cableado su UnityEvent manualmente en el Inspector.
            int indice = i;
            p.pieza.onPiezaColocada.AddListener(() => RegistrarPiezaColocada(indice));

            // Por defecto el modelo se ve armado: cada pieza arranca en su
            // posición correcta (Snap) y sin poder arrastrarse todavía.
            p.pieza.transform.position = p.pieza.snapPosition.position;
            p.pieza.transform.rotation = p.pieza.snapPosition.rotation;
            p.pieza.enabled = false;

            indicadores[i] = CrearIndicador(p);
        }
    }

    private GameObject CrearIndicador(PiezaDesarme p)
    {
        if (materialIndicador == null) return null;

        GameObject ind = GameObject.CreatePrimitive(PrimitiveType.Cube);
        ind.name = "Indicador_" + p.pieza.name;
        ind.transform.SetParent(p.pieza.snapPosition, worldPositionStays: false);
        ind.transform.localPosition = Vector3.zero;
        ind.transform.localRotation = Quaternion.identity;
        ind.transform.localScale = Vector3.one * tamanoIndicador;

        Collider col = ind.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer rend = ind.GetComponent<Renderer>();
        if (rend != null) rend.sharedMaterial = materialIndicador;

        ind.SetActive(false);
        return ind;
    }

    public void IniciarJuego()
    {
        if (panelJuego != null) panelJuego.SetActive(true);
        if (panelCompletado != null) panelCompletado.SetActive(false);

        piezasColocadas = 0;
        juegoIniciado = true;

        for (int i = 0; i < piezas.Length; i++)
        {
            PiezaDesarme p = piezas[i];
            if (p.pieza == null || p.posDesarmada == null) continue;

            // Dispersa la pieza y habilita el arrastre
            p.pieza.ReiniciarPieza(p.posDesarmada.position, p.posDesarmada.rotation);

            if (indicadores[i] != null) indicadores[i].SetActive(true);
        }

        Debug.Log($"{name}: desarme iniciado con {piezas.Length} piezas.");
    }

    private void RegistrarPiezaColocada(int indice)
    {
        if (!juegoIniciado) return;

        piezasColocadas++;
        Debug.Log($"{name}: pieza colocada ({piezasColocadas}/{piezas.Length}).");

        if (indicadores[indice] != null) indicadores[indice].SetActive(false);

        if (piezasColocadas >= piezas.Length)
        {
            Completar();
        }
    }

    private void Completar()
    {
        juegoIniciado = false;
        Debug.Log($"{name}: ¡modelo reensamblado correctamente!");

        if (panelCompletado != null)
        {
            panelCompletado.SetActive(true);
        }
    }
}
