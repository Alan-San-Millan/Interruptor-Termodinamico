using UnityEngine;
using UnityEngine.Events;

// Etapa de "desarmado del interruptor": las piezas parten armadas (cada una
// en su posición correcta) y, al presionar el botón, saltan a una posición
// dispersa. El usuario debe arrastrar cada una de vuelta a su lugar.
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

    private int piezasColocadas = 0;
    private bool juegoIniciado = false;

    void Awake()
    {
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
            p.pieza.onPiezaColocada.AddListener(RegistrarPiezaColocada);

            // Por defecto el modelo se ve armado: cada pieza arranca en su
            // posición correcta (Snap) y sin poder arrastrarse todavía.
            p.pieza.transform.position = p.pieza.snapPosition.position;
            p.pieza.transform.rotation = p.pieza.snapPosition.rotation;
            p.pieza.enabled = false;
        }
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
        }

        Debug.Log($"{name}: desarme iniciado con {piezas.Length} piezas.");
    }

    private void RegistrarPiezaColocada()
    {
        if (!juegoIniciado) return;

        piezasColocadas++;
        Debug.Log($"{name}: pieza colocada ({piezasColocadas}/{piezas.Length}).");

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
