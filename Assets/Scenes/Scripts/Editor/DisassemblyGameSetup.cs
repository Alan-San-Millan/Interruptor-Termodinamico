using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

// Herramienta de Editor para configurar automáticamente el juego de desarme
// (etapa de corto circuito) a partir de la convención de nombres del proyecto:
// una pieza llamada "PIEZAx" (o "piezax") con un objeto hermano "Snap_PIEZAx"
// que marca su posición correcta.
public static class DisassemblyGameSetup
{
    private static readonly Regex PiezaRegex = new Regex(@"^pieza\d+$", RegexOptions.IgnoreCase);

    // Separación entre piezas dispersas, en unidades del mundo
    private const float EspaciadoGrilla = 2.5f;

    [MenuItem("Tools/Interruptor/Configurar Juego de Desarme")]
    public static void ConfigurarJuegoDeDesarme()
    {
        // 0. Piezas exclusivas de otras animaciones (Sobrecarga / Corto Circuito
        // "lever trip"): no deben tocarse ni incluirse en el juego de desarme.
        HashSet<string> nombresExcluidos = ObtenerNombresUsadosPorOtrosAnimadores();

        // 1. Buscamos todas las piezas y sus Snap_ correspondientes en toda la escena
        Transform[] todos = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

        var piezasEncontradas = new List<(Transform pieza, Transform snap)>();

        foreach (Transform t in todos)
        {
            if (!PiezaRegex.IsMatch(t.name)) continue;
            if (t.parent == null) continue;

            // Las piezas ORIGINALES (fijas, siempre visibles) están anidadas
            // dentro de "piezaFinal". Las que nos interesan para el juego son
            // las copias sueltas fuera de ese objeto.
            if (EstaDentroDe(t, "piezaFinal"))
            {
                continue;
            }

            if (nombresExcluidos.Contains(t.name.ToLowerInvariant()))
            {
                // Es una copia de una pieza que ya anima Sobrecarga/CortoCircuito:
                // la ocultamos (es redundante) y no la tocamos para nada más.
                if (t.gameObject.activeSelf)
                {
                    Undo.RecordObject(t.gameObject, "Ocultar copia redundante");
                    t.gameObject.SetActive(false);
                }
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
                Debug.LogWarning($"DisassemblyGameSetup: no se encontró 'Snap_{t.name}' junto a {t.name}, se omite.");
                continue;
            }

            piezasEncontradas.Add((t, snap));
        }

        // Re-habilitamos cualquier pieza original excluida que haya quedado
        // mal oculta por una corrida anterior de esta herramienta.
        foreach (GameObject piezaFinalGO in ObjetosLlamados("piezaFinal"))
        {
            foreach (string nombreExcluido in nombresExcluidos)
            {
                Transform original = BuscarHijoPorNombre(piezaFinalGO.transform, nombreExcluido);
                if (original != null && !original.gameObject.activeSelf)
                {
                    Undo.RecordObject(original.gameObject, "Reactivar pieza original excluida");
                    original.gameObject.SetActive(true);
                }
            }
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

        // Centro de referencia (promedio de los snaps) para ubicar la grilla
        // de piezas dispersas separada del modelo armado.
        Vector3 centro = Vector3.zero;
        foreach (var (_, snap) in piezasEncontradas) centro += snap.position;
        centro /= piezasEncontradas.Count;

        // Ocultamos (para siempre) las piezas originales dentro de piezaFinal:
        // las copias sueltas ya cumplen su rol, tanto armadas como dispersas.
        int originalesOcultadas = 0;
        foreach (GameObject piezaFinalGO in ObjetosLlamados("piezaFinal"))
        {
            foreach (var (pieza, _) in piezasEncontradas)
            {
                Transform original = BuscarHijoPorNombre(piezaFinalGO.transform, pieza.name);
                if (original != null && original.gameObject.activeSelf)
                {
                    Undo.RecordObject(original.gameObject, "Ocultar pieza original");
                    original.gameObject.SetActive(false);
                    originalesOcultadas++;
                }
            }
        }
        Debug.Log($"DisassemblyGameSetup: {originalesOcultadas} piezas originales ocultadas dentro de piezaFinal.");

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
            // modelo armado, en vez de usar la pose actual (poco confiable).
            string nombreDesarmada = "Desarmada_" + pieza.name;
            Transform posDesarmada = contenedorDesarmadas.transform.Find(nombreDesarmada);
            if (posDesarmada == null)
            {
                GameObject go = new GameObject(nombreDesarmada);
                Undo.RegisterCreatedObjectUndo(go, "Crear " + nombreDesarmada);
                go.transform.SetParent(contenedorDesarmadas.transform, worldPositionStays: false);
                posDesarmada = go.transform;
            }

            int fila = indice / columnas;
            int columna = indice % columnas;
            Vector3 offset = new Vector3(
                (columna - (columnas - 1) / 2f) * EspaciadoGrilla,
                fila * EspaciadoGrilla,
                -EspaciadoGrilla * columnas // las alejamos hacia la cámara/al frente
            );

            posDesarmada.position = centro + offset;
            posDesarmada.rotation = Quaternion.identity;
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

    private static HashSet<string> ObtenerNombresUsadosPorOtrosAnimadores()
    {
        var nombres = new HashSet<string>();

        foreach (SwitchOverloadAnimator anim in Object.FindObjectsByType<SwitchOverloadAnimator>(FindObjectsSortMode.None))
        {
            if (anim.piezas == null) continue;
            foreach (var p in anim.piezas)
            {
                if (p.pieza != null) nombres.Add(p.pieza.name.ToLowerInvariant());
            }
        }

        foreach (ShortCircuitAnimator anim in Object.FindObjectsByType<ShortCircuitAnimator>(FindObjectsSortMode.None))
        {
            if (anim.piezas == null) continue;
            foreach (var p in anim.piezas)
            {
                if (p.pieza != null) nombres.Add(p.pieza.name.ToLowerInvariant());
            }
        }

        return nombres;
    }

    private static bool EstaDentroDe(Transform t, string nombreAncestro)
    {
        for (Transform p = t.parent; p != null; p = p.parent)
        {
            if (p.name.Equals(nombreAncestro, System.StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    private static IEnumerable<GameObject> ObjetosLlamados(string nombre)
    {
        foreach (Transform t in Object.FindObjectsByType<Transform>(FindObjectsSortMode.None))
        {
            if (t.name.Equals(nombre, System.StringComparison.OrdinalIgnoreCase))
            {
                yield return t.gameObject;
            }
        }
    }

    private static Transform BuscarHijoPorNombre(Transform raiz, string nombre)
    {
        foreach (Transform hijo in raiz)
        {
            if (hijo.name.Equals(nombre, System.StringComparison.OrdinalIgnoreCase))
            {
                return hijo;
            }

            Transform enHijo = BuscarHijoPorNombre(hijo, nombre);
            if (enHijo != null) return enHijo;
        }
        return null;
    }
}
