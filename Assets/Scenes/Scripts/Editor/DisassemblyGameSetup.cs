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
                // parte del juego, no hace falta avisar.
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
            // Tolerancia proporcional al modelo: con un valor fijo, en un
            // modelo chico encajaría todo de una y en uno grande nada.
            drag.positionTolerance = Mathf.Max(boundsModelo.size.magnitude * 0.05f, 0.05f);

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
        Material existente = AssetDatabase.LoadAssetAtPath<Material>(RutaMaterialIndicador);
        if (existente != null) return existente;

        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader == null)
        {
            Debug.LogWarning("DisassemblyGameSetup: no se encontró un shader Unlit para crear el material del punto.");
            return null;
        }

        Material mat = new Material(shader) { name = "Mat_PuntoDestino" };

        // Verde plano, dibujado por encima del resto para que no quede tapado
        // por la carcasa ni por otras piezas.
        Color verde = new Color(0.15f, 0.9f, 0.25f, 1f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", verde);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", verde);
        if (mat.HasProperty("_ZTest")) mat.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay;

        AssetDatabase.CreateAsset(mat, RutaMaterialIndicador);
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
