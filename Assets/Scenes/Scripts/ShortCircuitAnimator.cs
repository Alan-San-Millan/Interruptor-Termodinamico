using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

public class ShortCircuitAnimator : MonoBehaviour
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

    [Header("Configuración del disparo")]
    [Tooltip("Segundos que tarda el golpe magnético en abrir el switch (un corto circuito es casi instantáneo)")]
    public float duracionDisparo = 0.08f;

    [Header("Destello (opcional)")]
    [Tooltip("Imagen UI blanca a pantalla completa, con alpha en 0, usada para el flash")]
    public Image destello;

    [Tooltip("Segundos que tarda en aparecer y desaparecer el destello")]
    public float duracionDestello = 0.15f;

    [Header("Sacudida de cámara (opcional)")]
    public Transform camara;
    public float intensidadSacudida = 0.12f;
    public float duracionSacudida = 0.18f;

    [Header("Eventos")]
    [Tooltip("Se dispara cuando termina la animación del disparo (golpe magnético)")]
    public UnityEvent onCortoCircuitoFinalizado;

    private Coroutine animacionActual;

    public void IniciarCortoCircuito()
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
            Debug.LogWarning($"{name}: este GameObject está inactivo, por eso no puede correr la coroutine de animación. Activalo (o activá al panel que lo contiene) antes de llamar a IniciarCortoCircuito().");
            return;
        }

        if (animacionActual != null)
        {
            StopCoroutine(animacionActual);
        }
        animacionActual = StartCoroutine(AnimarCortoCircuito());
    }

    private IEnumerator AnimarCortoCircuito()
    {
        // Nos aseguramos de arrancar en la posición "encendido"
        AplicarDestinoInmediato(apagado: false);

        if (destello != null) StartCoroutine(AnimarDestello());
        if (camara != null) StartCoroutine(AnimarSacudida());

        // El golpe magnético abre el switch casi de inmediato, sin fase de calentamiento
        yield return MoverPiezas(apagado: true, duracion: duracionDisparo);

        animacionActual = null;
        Debug.Log("Simulación de corto circuito finalizada.");
        onCortoCircuitoFinalizado?.Invoke();
    }

    private IEnumerator AnimarDestello()
    {
        Color c = destello.color;

        float t = 0f;
        float mitad = duracionDestello * 0.5f;

        while (t < mitad)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0f, 0.9f, t / mitad);
            destello.color = c;
            yield return null;
        }

        t = 0f;
        while (t < mitad)
        {
            t += Time.deltaTime;
            c.a = Mathf.Lerp(0.9f, 0f, t / mitad);
            destello.color = c;
            yield return null;
        }

        c.a = 0f;
        destello.color = c;
    }

    private IEnumerator AnimarSacudida()
    {
        Vector3 posOriginal = camara.localPosition;
        float t = 0f;

        while (t < duracionSacudida)
        {
            t += Time.deltaTime;
            float atenuacion = 1f - (t / duracionSacudida);
            Vector2 offset = Random.insideUnitCircle * intensidadSacudida * atenuacion;
            camara.localPosition = posOriginal + new Vector3(offset.x, offset.y, 0f);
            yield return null;
        }

        camara.localPosition = posOriginal;
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
