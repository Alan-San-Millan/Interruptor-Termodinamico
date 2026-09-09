using System.Collections;
using UnityEngine;

public class SwitchOverloadAnimator : MonoBehaviour
{
    [System.Serializable]
    public class PiezaAnimada
    {
        [Tooltip("La pieza (parte del modelo) que se va a mover")]
        public Transform pieza;

        [Tooltip("Objeto vacío colocado en la posición/rotación de 'encendido' de esta pieza")]
        public Transform posEncendido;

        [Tooltip("Objeto vacío colocado en la posición/rotación de 'apagado' de esta pieza")]
        public Transform posApagado;
    }

    [Header("Piezas a animar")]
    public PiezaAnimada[] piezas;

    [Header("Simulación de calentamiento (opcional)")]
    [Tooltip("Renderers de las piezas que se van 'calentando' antes de disparar (ej: la lámina bimetálica)")]
    public Renderer[] piezasQueSeCalientan;

    [Tooltip("Color normal antes de la sobrecarga")]
    public Color colorFrio = Color.white;

    [Tooltip("Color al que llega justo antes de disparar (simula el calor)")]
    public Color colorCaliente = new Color(1f, 0.25f, 0f);

    [Tooltip("Segundos que tarda en calentarse antes de disparar (0 = sin fase de calentamiento)")]
    public float duracionCalentamiento = 1.5f;

    [Header("Configuración del disparo")]
    [Tooltip("Segundos que tarda la pieza en pasar de encendido a apagado")]
    public float duracionMovimiento = 0.35f;

    [Tooltip("Cuántas veces se repite el ciclo completo (calentamiento + disparo). 1 = una sola simulación")]
    public int repeticiones = 1;

    [Tooltip("Si está activo, el ciclo se repite indefinidamente hasta llamar a DetenerSimulacion()")]
    public bool repetirIndefinidamente = false;

    private static readonly int ColorPropId = Shader.PropertyToID("_BaseColor");
    private static readonly int ColorPropIdLegacy = Shader.PropertyToID("_Color");
    private MaterialPropertyBlock propBlock;

    private Coroutine animacionActual;

    public void IniciarSobrecarga()
    {
        if (piezas == null || piezas.Length == 0)
        {
            Debug.LogWarning($"{name}: el array 'Piezas' está vacío. Agregá al menos una pieza con su Pieza/Pos Encendido/Pos Apagado en el Inspector.");
            return;
        }

        for (int i = 0; i < piezas.Length; i++)
        {
            PiezaAnimada p = piezas[i];
            if (p.pieza == null || p.posEncendido == null || p.posApagado == null)
            {
                Debug.LogWarning($"{name}: el elemento {i} de 'Piezas' tiene una referencia sin asignar (Pieza/Pos Encendido/Pos Apagado) y no va a animarse.");
            }
        }

        if (!gameObject.activeInHierarchy)
        {
            Debug.LogWarning($"{name}: este GameObject está inactivo, por eso no puede correr la coroutine de animación. Activalo (o activá al panel que lo contiene) antes de llamar a IniciarSobrecarga().");
            return;
        }

        if (animacionActual != null)
        {
            StopCoroutine(animacionActual);
        }
        animacionActual = StartCoroutine(AnimarSobrecarga());
    }

    public void DetenerSimulacion()
    {
        if (animacionActual != null)
        {
            StopCoroutine(animacionActual);
            animacionActual = null;
        }
    }

    private IEnumerator AnimarSobrecarga()
    {
        int ciclos = 0;
        while (repetirIndefinidamente || ciclos < repeticiones)
        {
            // Aseguramos que arranca en la posición "encendido"
            yield return MoverPiezas(apagado: false, duracion: duracionMovimiento * 0.5f);

            // Fase 1: la lámina bimetálica se va calentando (sube la temperatura, sin moverse todavía)
            if (duracionCalentamiento > 0f)
            {
                yield return Calentar();
            }

            // Fase 2: dispara y pasa a apagado
            yield return MoverPiezas(apagado: true, duracion: duracionMovimiento);

            EnfriarInstantaneo();
            ciclos++;
        }

        animacionActual = null;
        Debug.Log("Simulación de sobrecarga finalizada.");
    }

    private IEnumerator Calentar()
    {
        if (propBlock == null) propBlock = new MaterialPropertyBlock();

        float t = 0f;
        while (t < duracionCalentamiento)
        {
            t += Time.deltaTime;
            float progreso = Mathf.Clamp01(t / duracionCalentamiento);
            Color actual = Color.Lerp(colorFrio, colorCaliente, progreso);
            AplicarColor(actual);
            yield return null;
        }
    }

    private void AplicarColor(Color color)
    {
        if (piezasQueSeCalientan == null) return;

        for (int i = 0; i < piezasQueSeCalientan.Length; i++)
        {
            Renderer r = piezasQueSeCalientan[i];
            if (r == null) continue;

            r.GetPropertyBlock(propBlock);
            propBlock.SetColor(ColorPropId, color);
            propBlock.SetColor(ColorPropIdLegacy, color);
            r.SetPropertyBlock(propBlock);
        }
    }

    private void EnfriarInstantaneo()
    {
        AplicarColor(colorFrio);
    }

    private IEnumerator MoverPiezas(bool apagado, float duracion)
    {
        if (duracion <= 0f)
        {
            AplicarDestinoInmediato(apagado);
            yield break;
        }

        float t = 0f;

        Vector3[] posInicial = new Vector3[piezas.Length];
        Quaternion[] rotInicial = new Quaternion[piezas.Length];

        for (int i = 0; i < piezas.Length; i++)
        {
            if (piezas[i].pieza == null) continue;
            posInicial[i] = piezas[i].pieza.localPosition;
            rotInicial[i] = piezas[i].pieza.localRotation;
        }

        while (t < duracion)
        {
            t += Time.deltaTime;
            float progreso = Mathf.Clamp01(t / duracion);

            for (int i = 0; i < piezas.Length; i++)
            {
                PiezaAnimada p = piezas[i];
                if (p.pieza == null || p.posEncendido == null || p.posApagado == null) continue;

                Transform destino = apagado ? p.posApagado : p.posEncendido;
                p.pieza.localPosition = Vector3.Lerp(posInicial[i], destino.localPosition, progreso);
                p.pieza.localRotation = Quaternion.Slerp(rotInicial[i], destino.localRotation, progreso);
            }

            yield return null;
        }

        AplicarDestinoInmediato(apagado);
    }

    private void AplicarDestinoInmediato(bool apagado)
    {
        for (int i = 0; i < piezas.Length; i++)
        {
            PiezaAnimada p = piezas[i];
            if (p.pieza == null || p.posEncendido == null || p.posApagado == null) continue;

            Transform destino = apagado ? p.posApagado : p.posEncendido;
            p.pieza.localPosition = destino.localPosition;
            p.pieza.localRotation = destino.localRotation;
        }
    }
}
