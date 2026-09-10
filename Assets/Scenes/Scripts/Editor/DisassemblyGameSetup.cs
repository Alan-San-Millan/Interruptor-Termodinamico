using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Herramienta de Editor para configurar automáticamente el juego de desarme
// (etapa de corto circuito) a partir de la convención de nombres del proyecto:
// una pieza llamada "PIEZAx" (o "piezax", dentro de piezaFinal) con un objeto
// hermano "Snap_PIEZAx" que marca su posición correcta.
public static class DisassemblyGameSetup
{
    private static readonly Regex PiezaRegex = new Regex(@"^pieza\d+$", RegexOptions.IgnoreCase);

    private const string RutaMaterialIndicador = "Assets/Switch/Mat_IndicadorDesarme.mat";

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

        // Calculamos el tamaño real de la carcasa/modelo armado (bounds de
        // todos sus renderers) para poder ubicar las piezas dispersas
        // claramente AFUERA de ella, con un espaciado proporcional a su
        // escala real (no un valor fijo que puede quedar minúsculo o gigante
        // según las unidades del modelo).
        Bounds boundsModelo = CalcularBoundsModelo(piezasEncontradas);

        // Arrancamos la grilla justo a la derecha del borde derecho del
        // modelo, con margen proporcional a su tamaño.
        float margen = boundsModelo.extents.x * 0.4f + 0.1f;
        float espaciado = Mathf.Max(boundsModelo.size.magnitude * 0.12f, margen * 0.5f);
        Vector3 origen = new Vector3(boundsModelo.max.x + margen, boundsModelo.min.y, 0f);

        var piezasParaManager = new List<DisassemblyGame.PiezaDesarme>();

        int columnas = Mathf.CeilToInt(Mathf.Sqrt(piezasEncontradas.Count));
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

            // Solo se corren en X/Y: conservan su propia profundidad (Z) y su
            // rotación de armado, como si las hubieran sacado del interruptor
            // y las dejaran flotando afuera de la carcasa, mirando igual que
            // antes.
            int fila = indice / columnas;
            int columna = indice % columnas;

            posDesarmada.position = new Vector3(
                origen.x + columna * espaciado,
                origen.y + fila * espaciado,
                snap.position.z
            );
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

        if (manager.materialIndicador == null)
        {
            manager.materialIndicador = ObtenerOCrearMaterialIndicador();
        }

        EditorUtility.SetDirty(manager);

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

        Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
        if (shader == null)
        {
            Debug.LogWarning("DisassemblyGameSetup: no se encontró un shader URP/Standard para crear el material indicador.");
            return null;
        }

        Material mat = new Material(shader) { name = "Mat_IndicadorDesarme" };

        // Configuración de transparencia (URP Lit)
        mat.SetFloat("_Surface", 1f); // Transparent
        mat.SetFloat("_Blend", 0f);   // Alpha
        mat.SetOverrideTag("RenderType", "Transparent");
        mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        mat.SetInt("_ZWrite", 0);
        mat.DisableKeyword("_ALPHATEST_ON");
        mat.EnableKeyword("_ALPHABLEND_ON");
        mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        Color colorIndicador = new Color(0.3f, 0.8f, 1f, 0.35f);
        if (mat.HasProperty("_BaseColor")) mat.SetColor("_BaseColor", colorIndicador);
        if (mat.HasProperty("_Color")) mat.SetColor("_Color", colorIndicador);

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
