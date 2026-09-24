using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText; // Asigna tu ScoreText aquí
    private int score = 0;

    // Piezas colocadas correctamente, sin importar los puntos perdidos por
    // error: determina cuándo termina el juego, separado del puntaje.
    private int piezasColocadas = 0;

    [Header("Configuración del Juego")]
    [Tooltip("Cantidad total de etiquetas a colocar")]
    public int piezasTotales = 4; 

    [Header("Contenedores Principales de Etapas")]
    [Tooltip("Arrastra aquí el objeto padre que creaste para agrupar el primer juego")]
    public GameObject juegoUnirPiezas;  // <-- NUEVO: Objeto padre del juego 1

    [Header("Paneles de Interfaz extras")]
    public GameObject panelVictoria;      // Arrastra aquí el 'PanelVictoria'
    public GameObject panelSobrecarga;    // Arrastra aquí el 'PanelSobrecarga'
    public GameObject panelCortoCircuito; // Arrastra aquí el 'PanelCortoCircuito'
    public GameObject panelUnirPiezas;    // Panel del botón que abre el juego de nombres

    [Header("Animación de Sobrecarga")]
    [Tooltip("Arrastra aquí el objeto que tiene el componente SwitchOverloadAnimator")]
    public SwitchOverloadAnimator overloadAnimator;

    [Header("Animación de Corto Circuito")]
    [Tooltip("Arrastra aquí el objeto que tiene el componente ShortCircuitAnimator")]
    public ShortCircuitAnimator shortCircuitAnimator;

    [Header("Juego de Desarme (tras el Corto Circuito)")]
    [Tooltip("Arrastra aquí el objeto que tiene el componente DisassemblyGame")]
    public DisassemblyGame disassemblyGame;

    void Start()
    {
        // El juego arranca en la pantalla de selección: el jugador elige etapa
        VolverASeleccion();
    }

    public void AddScore()
    {
        score = Mathf.Min(score + 1, piezasTotales);
        piezasColocadas++;
        ActualizarScore();

        // El juego termina al colocar todas las piezas, gane o no el puntaje
        // máximo: los errores restan puntos pero no impiden terminar la partida.
        if (piezasColocadas >= piezasTotales)
        {
            GanarJuego();
        }
    }

    // La llama una etiqueta al soltarse sobre el destino de OTRA etiqueta. El
    // puntaje nunca baja de 0: un error no puede dejar al jugador en negativo.
    public void PerderPunto()
    {
        score = Mathf.Max(score - 1, 0);
        ActualizarScore();
    }

    private void ActualizarScore()
    {
        if (scoreText != null)
        {
            scoreText.text = $"Puntos: {score}/{piezasTotales}";
        }
    }

    private void GanarJuego()
    {
        Debug.Log("¡Todas las piezas colocadas!");
        if (panelVictoria != null)
        {
            panelVictoria.SetActive(true);
        }
    }

    // Pantalla donde el jugador elige etapa. La usan el arranque del juego y el
    // botón "Siguiente etapa" del panel de victoria.
    public void VolverASeleccion()
    {
        if (juegoUnirPiezas != null) juegoUnirPiezas.SetActive(false);
        if (panelVictoria != null) panelVictoria.SetActive(false);

        MostrarSeleccion(true);
    }

    private void MostrarSeleccion(bool visible)
    {
        if (panelSobrecarga != null) panelSobrecarga.SetActive(visible);
        if (panelCortoCircuito != null) panelCortoCircuito.SetActive(visible);
        if (panelUnirPiezas != null) panelUnirPiezas.SetActive(visible);
    }

    // Botón que abre el juego de colocar los nombres sobre las piezas
    public void PresionarBotonUnirPiezas()
    {
        MostrarSeleccion(false);
        if (panelVictoria != null) panelVictoria.SetActive(false);

        score = 0;
        piezasColocadas = 0;
        ActualizarScore();

        if (juegoUnirPiezas != null)
        {
            juegoUnirPiezas.SetActive(true);

            // Devolvemos las etiquetas a su lugar para poder volver a jugarlo
            foreach (DragAndDrop etiqueta in juegoUnirPiezas.GetComponentsInChildren<DragAndDrop>(true))
            {
                etiqueta.ReiniciarEtiqueta();
            }
        }

        Debug.Log("Iniciando juego de unir piezas...");
    }

    public void PresionarBotonSobrecarga()
    {
        if (overloadAnimator == null)
        {
            Debug.LogWarning("PresionarBotonSobrecarga: no hay ningún 'Overload Animator' asignado en el GameManager (Inspector). La animación no puede iniciar.");
            return;
        }

        Debug.Log("Iniciando animación de sobrecarga...");
        overloadAnimator.IniciarSobrecarga();
    }

    // Botón "Desarmado de interruptor": sin animación, muestra directamente
    // las piezas dispersas para que el usuario las vuelva a colocar.
    public void PresionarBotonCortoCircuito()
    {
        if (disassemblyGame == null)
        {
            Debug.LogWarning("PresionarBotonCortoCircuito: no hay ningún 'Disassembly Game' asignado en el GameManager (Inspector). El juego de desarme no puede iniciar.");
            return;
        }

        Debug.Log("Iniciando juego de desarme...");
        disassemblyGame.IniciarJuego();
    }
}