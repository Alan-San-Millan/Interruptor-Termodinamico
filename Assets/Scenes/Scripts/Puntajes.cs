using UnityEngine;

// Guarda entre sesiones el resultado de cada minijuego, junto con el máximo que
// se podía sacar, para poder mostrarlos como "obtenido/máximo" en el menú.
// Se queda con el último intento: volver a jugar actualiza el número.
public static class Puntajes
{
    public const string UnirPiezas = "UnirPiezas";
    public const string Desarme = "Desarme";

    public static void Guardar(string juego, int puntaje, int maximo)
    {
        PlayerPrefs.SetInt(ClavePuntaje(juego), puntaje);
        PlayerPrefs.SetInt(ClaveMaximo(juego), maximo);
        PlayerPrefs.Save();
    }

    public static bool TieneResultado(string juego)
    {
        return PlayerPrefs.HasKey(ClavePuntaje(juego));
    }

    public static int Puntaje(string juego)
    {
        return PlayerPrefs.GetInt(ClavePuntaje(juego), 0);
    }

    public static int Maximo(string juego)
    {
        return PlayerPrefs.GetInt(ClaveMaximo(juego), 0);
    }

    public static void BorrarTodo()
    {
        foreach (string juego in new[] { UnirPiezas, Desarme })
        {
            PlayerPrefs.DeleteKey(ClavePuntaje(juego));
            PlayerPrefs.DeleteKey(ClaveMaximo(juego));
        }
        PlayerPrefs.Save();
    }

    private static string ClavePuntaje(string juego) => "Puntaje_" + juego;
    private static string ClaveMaximo(string juego) => "Maximo_" + juego;
}
