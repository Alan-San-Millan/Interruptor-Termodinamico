using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;

// Herramienta de Editor para configurar automáticamente el juego de desarme
// (etapa de corto circuito) a partir de la convención de nombres del proyecto:
// una pieza llamada "PIEZAx" (o "piezax", dentro de piezaFinal) con un objeto
// hermano "Snap_PIEZAx" que marca su posición correcta.
public static class DisassemblyGameSetup
{
    private static readonly Regex PiezaRegex = new Regex(@"^pieza\d+$", RegexOptions.IgnoreCase);

    private const string RutaMaterialIndicador = "Assets/Switch/Mat_PuntoDestino.mat";

    // Dónde se apilan las piezas sueltas, en coordenadas de pantalla (0 = borde
    // izquierdo, 1 = borde derecho). Ajustá estos valores si las piezas tapan
    // los botones de la interfaz o quedan muy al borde.
    private static readonly float[] ColumnasIzquierda = { 0.07f, 0.19f };
    private static readonly float[] ColumnasDerecha = { 0.66f, 0.79f };
    private const float AlturaMinima = 0.15f;
    private const float AlturaMaxima = 0.85f;

    [MenuItem("Tools/Interruptor/Configurar Juego de Desarme")]
    public static void ConfigurarJuegoDeDesarme()
    {
        // 0. Piezas exclusivas de otras animaciones (Sobrecarga / Corto Circuito
        // "lever trip"): no deben tocarse ni incluirse en el juego de desarme.
        HashSet<string> nombresExcluidos = ObtenerNombresUsadosPorOtrosAnimadores();

        // 1. Buscamos TODAS las piezas (incluidas las inactivas, por si quedaron
        // mal apagadas por una corrida anterior de esta herramienta) y sus
        // Snap_ correspondientes.
        Transform[] todos = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        var piezasEncontradas = new List<(Transform pieza, Transform snap)>();
        var piezasFijas = new List<Transform>();
        int reactivadas = 0;

        foreach (Transform t in todos)
        {
            if (!PiezaRegex.IsMatch(t.name)) continue;
            if (t.parent == null) continue;

            bool esExcluida = nombresExcluidos.Contains(t.name.ToLowerInvariant());

            // Si quedó apagada por error (por ejemplo, por una corrida anterior
            // de esta misma herramienta), la reactivamos siempre.
            if (!t.gameObject.activeSelf)
            {
                Undo.RecordObject(t.gameObject, "Reactivar pieza");
                t.gameObject.SetActive(true);
                reactivadas++;
            }

            if (esExcluida)
            {
                // Es una pieza que ya controla Sobrecarga/CortoCircuito: la
                // dejamos intacta, no forma parte del juego de desarme.
                continue;
            }

            Transform snap = t.parent.Find("Snap_" + t.name);
            if (snap == null)
            {
                // Buscamos también ignorando mayúsculas/minúsculas
                foreach (Transform hermano in t.parent)
                {
                    if (hermano.name.Equals("Snap_" + t.name, System.StringComparison.OrdinalIgnoreCase))
                    {
                        snap = hermano;
                        break;
                    }
                }
            }

            if (snap == null)
            {
                // Piezas sin Snap_ (ej: carcasa delantera/trasera, piezas
                // irrelevantes) se consideran fijas a propósito: no forman
                // parte del juego y quedan siempre visibles.
                piezasFijas.Add(t);
                continue;
            }

            piezasEncontradas.Add((t, snap));
        }

        if (reactivadas > 0)
        {
            Debug.Log($"DisassemblyGameSetup: {reactivadas} pieza(s) reactivadas (habían quedado ocultas).");
        }

        if (piezasEncontradas.Count == 0)
        {
            Debug.LogWarning("DisassemblyGameSetup: no se encontró ninguna pieza con el patrón PIEZAx + Snap_PIEZAx.");
            return;
        }

        // 2. Buscamos (o creamos) el contenedor de posiciones dispersas
        GameObject contenedorDesarmadas = GameObject.Find("PosicionesDesarmadas");
        if (contenedorDesarmadas == null)
        {
            contenedorDesarmadas = new GameObject("PosicionesDesarmadas");
            Undo.RegisterCreatedObjectUndo(contenedorDesarmadas, "Crear PosicionesDesarmadas");
        }

        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("DisassemblyGameSetup: no hay ninguna cámara con el tag 'MainCamera'. No puedo ubicar las piezas en pantalla.");
            return;
        }

        // Las piezas se reparten mitad a la izquierda y mitad a la derecha de
        // la pantalla, calculadas en coordenadas de cámara para garantizar que
        // queden siempre dentro del campo visual.
        int cantidadIzquierda = Mathf.CeilToInt(piezasEncontradas.Count / 2f);

        Bounds boundsModelo = CalcularBoundsModelo(piezasEncontradas);
        float tamanoPunto = Mathf.Max(boundsModelo.size.magnitude * 0.012f, 0.01f);

        var piezasParaManager = new List<DisassemblyGame.PiezaDesarme>();

        int indice = 0;

        foreach (var (pieza, snap) in piezasEncontradas)
        {
            // Agregamos Piece3DDragAndDrop si no lo tiene, y le asignamos el snap
            Piece3DDragAndDrop drag = pieza.GetComponent<Piece3DDragAndDrop>();
            if (drag == null)
            {
                drag = Undo.AddComponent<Piece3DDragAndDrop>(pieza.gameObject);
            }
            drag.snapPosition = snap;

            // Verificamos que tenga collider (necesario para poder clickearla)
            if (pieza.GetComponent<Collider>() == null)
            {
                Debug.LogWarning($"DisassemblyGameSetup: {pieza.name} no tiene Collider, no se va a poder arrastrar.");
            }

            // Generamos la posición dispersa en una grilla, bien separada del
            // modelo armado.
            string nombreDesarmada = "Desarmada_" + pieza.name;
            Transform posDesarmada = contenedorDesarmadas.transform.Find(nombreDesarmada);
            if (posDesarmada == null)
            {
                GameObject go = new GameObject(nombreDesarmada);
                Undo.RegisterCreatedObjectUndo(go, "Crear " + nombreDesarmada);
                go.transform.SetParent(contenedorDesarmadas.transform, worldPositionStays: false);
                posDesarmada = go.transform;
            }

            // Ubicamos la pieza en pantalla: la primera mitad a la izquierda
            // del interruptor, la segunda mitad a la derecha. Conserva su
            // propia distancia a la cámara y su rotación de armado, así que
            // solo se corre lateral/verticalmente.
            bool esIzquierda = indice < cantidadIzquierda;
            float[] columnasLado = esIzquierda ? ColumnasIzquierda : ColumnasDerecha;

            int indiceEnLado = esIzquierda ? indice : indice - cantidadIzquierda;
            int cantidadEnLado = esIzquierda ? cantidadIzquierda : piezasEncontradas.Count - cantidadIzquierda;
            int filasPorLado = Mathf.CeilToInt(cantidadEnLado / (float)columnasLado.Length);

            int columna = indiceEnLado % columnasLado.Length;
            int fila = indiceEnLado / columnasLado.Length;

            float alturaViewport = filasPorLado <= 1
                ? (AlturaMinima + AlturaMaxima) * 0.5f
                : Mathf.Lerp(AlturaMaxima, AlturaMinima, fila / (float)(filasPorLado - 1));

            float profundidad = cam.WorldToViewportPoint(snap.position).z;
            posDesarmada.position = cam.ViewportToWorldPoint(
                new Vector3(columnasLado[columna], alturaViewport, profundidad));
            posDesarmada.rotation = snap.rotation;
            indice++;

            piezasParaManager.Add(new DisassemblyGame.PiezaDesarme
            {
                pieza = drag,
                posDesarmada = posDesarmada
            });
        }

        // 3. Buscamos (o creamos) el DesarmeManager y le cargamos la lista
        GameObject managerGO = GameObject.Find("DesarmeManager");
        if (managerGO == null)
        {
            managerGO = new GameObject("DesarmeManager");
            Undo.RegisterCreatedObjectUndo(managerGO, "Crear DesarmeManager");
        }

        DisassemblyGame manager = managerGO.GetComponent<DisassemblyGame>();
        if (manager == null)
        {
            manager = Undo.AddComponent<DisassemblyGame>(managerGO);
        }

        Undo.RecordObject(manager, "Configurar piezas del DesarmeManager");
        manager.piezas = piezasParaManager.ToArray();
        manager.materialIndicador = ObtenerOCrearMaterialIndicador();
        manager.tamanoIndicador = tamanoPunto;
        manager.objetosAOcultar = RecolectarObjetosAOcultar(piezasEncontradas, piezasFijas);

        if (manager.textoPuntaje == null)
        {
            manager.textoPuntaje = CrearTextoPuntaje();
        }

        EditorUtility.SetDirty(manager);

        // El arrastre 3D depende del EventSystem, así que la cámara necesita un
        // PhysicsRaycaster y ninguna UI invisible puede tapar la escena.
        PrepararEntradaDePuntero(cam);

        // 4. Conectamos el manager en el GameManager si existe en la escena
        GameManager gm = Object.FindAnyObjectByType<GameManager>();
        if (gm != null && gm.disassemblyGame == null)
        {
            Undo.RecordObject(gm, "Asignar DesarmeManager en GameManager");
            gm.disassemblyGame = manager;
            EditorUtility.SetDirty(gm);
        }

        Debug.Log($"DisassemblyGameSetup: configuradas {piezasParaManager.Count} piezas en '{managerGO.name}'.");
    }

    // Todo lo que debe desaparecer al empezar el juego: el mecanismo que queda
    // dentro de la carcasa (y que no es ni una pieza jugable ni la carcasa
    // misma) más los botones de la interfaz.
    private static GameObject[] RecolectarObjetosAOcultar(
        List<(Transform pieza, Transform snap)> piezasJugables, List<Transform> piezasFijas)
    {
        var raicesExcluidas = new HashSet<Transform>();
        foreach (var (pieza, _) in piezasJugables) raicesExcluidas.Add(pieza);
        foreach (Transform fija in piezasFijas) raicesExcluidas.Add(fija);

        var aOcultar = new List<GameObject>();

        Bounds boundsCarcasa = CalcularBoundsDeTransforms(piezasFijas);
        if (piezasFijas.Count > 0)
        {
            foreach (Renderer r in Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (TieneAncestroEn(r.transform, raicesExcluidas)) continue;
                if (!boundsCarcasa.Contains(r.bounds.center)) continue;
                // Descarta cosas enormes que engloban la carcasa (el piso, etc.)
                if (r.bounds.size.magnitude > boundsCarcasa.size.magnitude) continue;

                aOcultar.Add(r.gameObject);
            }
        }

        // Los botones tienen que salir de la pantalla mientras se juega
        foreach (string nombrePanel in new[] { "PanelSobrecarga", "PanelCortoCircuito" })
        {
            foreach (GameObject panel in ObjetosLlamados(nombrePanel))
            {
                if (!aOcultar.Contains(panel)) aOcultar.Add(panel);
            }
        }

        Debug.Log($"DisassemblyGameSetup: {aOcultar.Count} objeto(s) se ocultarán durante el juego (mecanismo interior + botones).");
        return aOcultar.ToArray();
    }

    private static bool TieneAncestroEn(Transform t, HashSet<Transform> raices)
    {
        for (Transform actual = t; actual != null; actual = actual.parent)
        {
            if (raices.Contains(actual)) return true;
        }
        return false;
    }

    private static Bounds CalcularBoundsDeTransforms(List<Transform> objetos)
    {
        Bounds? total = null;

        foreach (Transform t in objetos)
        {
            foreach (Renderer r in t.GetComponentsInChildren<Renderer>())
            {
                if (total == null) total = r.bounds;
                else { Bounds b = total.Value; b.Encapsulate(r.bounds); total = b; }
            }
        }

        return total ?? new Bounds(Vector3.zero, Vector3.zero);
    }

    private static TMPro.TextMeshProUGUI CrearTextoPuntaje()
    {
        Canvas canvas = Object.FindAnyObjectByType<Canvas>();
        if (canvas == null)
        {
            Debug.LogWarning("DisassemblyGameSetup: no hay Canvas en la escena, no pude crear el texto de puntaje.");
            return null;
        }

        var go = new GameObject("PuntajeDesarme", typeof(RectTransform));
        Undo.RegisterCreatedObjectUndo(go, "Crear PuntajeDesarme");
        go.transform.SetParent(canvas.transform, worldPositionStays: false);

        var rt = go.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0f, -30f);
        rt.sizeDelta = new Vector2(500f, 80f);

        var texto = go.AddComponent<TMPro.TextMeshProUGUI>();
        texto.text = "Puntuación: 0";
        texto.fontSize = 36f;
        texto.alignment = TMPro.TextAlignmentOptions.Center;
        texto.raycastTarget = false; // que no se robe los clicks

        Debug.Log("DisassemblyGameSetup: se creó el texto 'PuntajeDesarme' en el Canvas.");
        return texto;
    }

    // Sin PhysicsRaycaster en la cámara, los colliders 3D nunca reciben
    // eventos de puntero. Y una Image a pantalla completa con RaycastTarget
    // activo (aunque sea invisible) se come todos los clicks antes de que
    // lleguen al modelo.
    private static void PrepararEntradaDePuntero(Camera cam)
    {
        if (cam.GetComponent<PhysicsRaycaster>() == null)
        {
            Undo.AddComponent<PhysicsRaycaster>(cam.gameObject);
            Debug.Log($"DisassemblyGameSetup: se agregó un PhysicsRaycaster a '{cam.name}' (necesario para arrastrar piezas 3D).");
        }

        foreach (UnityEngine.UI.Image img in Object.FindObjectsByType<UnityEngine.UI.Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!img.raycastTarget) continue;
            if (img.GetComponent<UnityEngine.UI.Selectable>() != null) continue; // botones, etc: no tocar

            RectTransform rt = img.rectTransform;
            bool ocupaPantallaCompleta =
                rt.anchorMin == Vector2.zero && rt.anchorMax == Vector2.one &&
                rt.sizeDelta.x <= 0.01f && rt.sizeDelta.y <= 0.01f;

            if (ocupaPantallaCompleta && img.color.a <= 0.01f)
            {
                Undo.RecordObject(img, "Desbloquear raycast del panel");
                img.raycastTarget = false;
                EditorUtility.SetDirty(img);
                Debug.Log($"DisassemblyGameSetup: se desactivó 'Raycast Target' en '{img.name}' (imagen invisible a pantalla completa que bloqueaba los clicks).");
            }
        }
    }

    // Bounds combinado de todos los renderers de las piezas (mecanismo +
    // carcasa, ya que sus Renderer están dentro del mismo piezaFinal),
    // usado como referencia del tamaño real del modelo en el mundo.
    private static Bounds CalcularBoundsModelo(List<(Transform pieza, Transform snap)> piezas)
    {
        Bounds? total = null;

        // Encapsulamos las piezas del juego...
        foreach (var (pieza, _) in piezas)
        {
            foreach (Renderer r in pieza.GetComponentsInChildren<Renderer>())
            {
                if (total == null) total = r.bounds;
                else { Bounds b = total.Value; b.Encapsulate(r.bounds); total = b; }
            }
        }

        // ...y también toda la carcasa (piezaFinal completo), para no dejar
        // piezas dispersas encima de partes fijas como la tapa trasera.
        foreach (GameObject piezaFinalGO in ObjetosLlamados("piezaFinal"))
        {
            foreach (Renderer r in piezaFinalGO.GetComponentsInChildren<Renderer>())
            {
                if (total == null) total = r.bounds;
                else { Bounds b = total.Value; b.Encapsulate(r.bounds); total = b; }
            }
        }

        return total ?? new Bounds(Vector3.zero, Vector3.one);
    }

    private static IEnumerable<GameObject> ObjetosLlamados(string nombre)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.name.Equals(nombre, System.StringComparison.OrdinalIgnoreCase))
            {
                yield return t.gameObject;
            }
        }
    }

    private static Material ObtenerOCrearMaterialIndicador()
    {
        // Shader propio con ZTest Always: el punto nunca queda tapado
        Shader shader = Shader.Find("Custom/PuntoDestino") ?? Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
        {
            Debug.LogWarning("DisassemblyGameSetup: no se encontró un shader para crear el material del punto.");
            return null;
        }

        // Si ya existe de una corrida anterior, lo actualizamos en vez de
        // reutilizarlo con el shader viejo.
        Material mat = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialIndicador);
        bool esNuevo = mat == null;
        if (esNuevo) mat = new Material(shader) { name = "Mat_PuntoDestino" };
        else if (mat.shader != shader) mat.shader = shader;

        Color verde = new Color(0.15f, 0.9f, 0.25f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", verde);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", verde);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;

        if (esNuevo) AssetDatabase.CreateAsset(mat, RutaMaterialIndicador);
        else EditorUtility.SetDirty(mat);
        AssetDatabase.SaveAssets();

        return mat;
    }

    private static HashSet<string> ObtenerNombresUsadosPorOtrosAnimadores()
    {
        var nombres = new HashSet<string>();

        foreach (SwitchOverloadAnimator anim in Object.FindObjectsByType<SwitchOverloadAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (anim.piezas == null) continue;
            foreach (var p in anim.piezas)
            {
                if (p.pieza != null) nombres.Add(p.pieza.name.ToLowerInvariant());
            }
        }

        foreach (ShortCircuitAnimator anim in Object.FindObjectsByType<ShortCircuitAnimator>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (anim.piezas == null) continue;
            foreach (var p in anim.piezas)
            {
                if (p.pieza != null) nombres.Add(p.pieza.name.ToLowerInvariant());
            }
        }

        return nombres;
    }
}
