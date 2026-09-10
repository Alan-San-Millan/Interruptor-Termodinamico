using UnityEngine;
using UnityEngine.Events;

// Etapa de "armado tras corto circuito": el modelo queda desarmado (solo la
// carcasa) y el usuario debe arrastrar cada pieza a su posición correcta.
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

    [Header("Modelo armado (visual fijo)")]
    [Tooltip("Objeto(s) con el aspecto del interruptor ya armado (ej: piezaFinal). " +
             "Se ocultan al iniciar el desarme para que no se superpongan con las piezas sueltas.")]
    public GameObject[] modeloArmadoAOcultar;

    private int piezasColocadas = 0;
    private bool juegoIniciado = false;

    void Awake()
    {
        for (int i = 0; i < piezas.Length; i++)
        {
            PiezaDesarme p = piezas[i];
            if (p.pieza == null || p.posDesarmada == null)
            {
                Debug.LogWarning($"{name}: el elemento {i} de 'Piezas' tiene una referencia sin asignar (Pieza/Pos Desarmada).");
                continue;
            }

            // Nos suscribimos por código para no depender de que cada pieza
            // tenga cableado su UnityEvent manualmente en el Inspector.
            p.pieza.onPiezaColocada.AddListener(RegistrarPiezaColocada);

            // Las piezas sueltas están ocultas hasta que arranca el desarme,
            // para no verse superpuestas con el modelo armado.
            p.pieza.gameObject.SetActive(false);
        }
    }

    public void IniciarJuego()
    {
        if (panelJuego != null) panelJuego.SetActive(true);
        if (panelCompletado != null) panelCompletado.SetActive(false);

        for (int i = 0; i < modeloArmadoAOcultar.Length; i++)
        {
            if (modeloArmadoAOcultar[i] != null) modeloArmadoAOcultar[i].SetActive(false);
        }

        piezasColocadas = 0;
        juegoIniciado = true;

        for (int i = 0; i < piezas.Length; i++)
        {
            PiezaDesarme p = piezas[i];
            if (p.pieza == null || p.posDesarmada == null) continue;

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
