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

    [MenuItem("Tools/Interruptor/Configurar Juego de Desarme")]
    public static void ConfigurarJuegoDeDesarme()
    {
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

            // Creamos (o reutilizamos) el vacío "Desarmada_PIEZAx" en la posición
            // ACTUAL de la pieza (asumimos que ahora mismo el modelo está en su
            // pose "desarmada"/dispersa, tal como se ve en la escena)
            string nombreDesarmada = "Desarmada_" + pieza.name;
            Transform posDesarmada = contenedorDesarmadas.transform.Find(nombreDesarmada);
            if (posDesarmada == null)
            {
                GameObject go = new GameObject(nombreDesarmada);
                Undo.RegisterCreatedObjectUndo(go, "Crear " + nombreDesarmada);
                go.transform.SetParent(contenedorDesarmadas.transform, worldPositionStays: false);
                posDesarmada = go.transform;
            }
            posDesarmada.position = pieza.position;
            posDesarmada.rotation = pieza.rotation;

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
