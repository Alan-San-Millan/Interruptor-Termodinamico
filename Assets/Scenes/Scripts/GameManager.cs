using UnityEngine;
using TMPro;

public class GameManager : MonoBehaviour
{
    public TextMeshProUGUI scoreText; // Asigna tu ScoreText aquí
    private int score = 0;

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
        // Nos aseguramos de que los estados iniciales sean correctos
        if (juegoUnirPiezas != null) juegoUnirPiezas.SetActive(true);
        if (panelVictoria != null) panelVictoria.SetActive(false);
        if (panelSobrecarga != null) panelSobrecarga.SetActive(false);
        if (panelCortoCircuito != null) panelCortoCircuito.SetActive(false);

        // Cuando termina la animación del disparo, arrancamos el juego de desarme
        if (shortCircuitAnimator != null && disassemblyGame != null)
        {
            shortCircuitAnimator.onCortoCircuitoFinalizado.AddListener(disassemblyGame.IniciarJuego);
        }
    }

    public void AddScore()
    {
        score++;
        if (scoreText != null)
        {
            scoreText.text = "Puntuación: " + score;
        }

        if (score >= piezasTotales)
        {
            GanarJuego();
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

    // Esta función se ejecuta al presionar el botón "Siguiente etapa"
    public void CambiarAlCanvasSobrecarga()
    {
        // 1. Apagamos TODO el primer juego de golpe usando el objeto padre
        if (juegoUnirPiezas != null) 
        {
            juegoUnirPiezas.SetActive(false);
        }

        // 2. Apagamos el panel de victoria intermedio
        if (panelVictoria != null) 
        {
            panelVictoria.SetActive(false);
        }

        // 3. Encendemos el nuevo juego/etapa de sobrecarga
        if (panelSobrecarga != null)
        {
            panelSobrecarga.SetActive(true);
        }

        // 4. Encendemos también el panel de corto circuito
        if (panelCortoCircuito != null)
        {
            panelCortoCircuito.SetActive(true);
        }

        Debug.Log("Cambio de etapa completado con éxito.");
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

    public void PresionarBotonCortoCircuito()
    {
        if (shortCircuitAnimator == null)
        {
            Debug.LogWarning("PresionarBotonCortoCircuito: no hay ningún 'Short Circuit Animator' asignado en el GameManager (Inspector). La animación no puede iniciar.");
            return;
        }

        Debug.Log("Iniciando animación de corto circuito...");
        shortCircuitAnimator.IniciarCortoCircuito();
    }
}