using System.IO;

namespace Olympe.MaterialManager.Services;

/// <summary>
/// Service de logging fichier pour le diagnostic.
/// Ecrit dans %APPDATA%/Olympe/MaterialManager/olympe.log
/// FIA-11 : les traces verbeuses (une par requete/callback) sont coupees par
/// defaut via VerboseEnabled ; les erreurs sont TOUJOURS ecrites.
/// </summary>
public static class LogService
{
    private static readonly string _logPath;
    private static readonly object _lock = new();

    /// <summary>
    /// FIA-11 : active les traces verbeuses par requete (Log). Defaut false :
    /// en usage normal, seul Error ecrit dans le fichier. Passer a true pour
    /// une session de diagnostic.
    /// </summary>
    public static bool VerboseEnabled { get; set; }

    static LogService()
    {
        var dir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "Olympe", "MaterialManager");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "olympe.log");

        // Vider le log au demarrage
        try { File.WriteAllText(_logPath, $"=== Olympe MaterialManager Log - {DateTime.Now:yyyy-MM-dd HH:mm:ss} ===\n"); }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }

    /// <summary>
    /// Trace verbeuse (une par requete/callback). Ecrite uniquement si
    /// VerboseEnabled est actif (FIA-11).
    /// </summary>
    public static void Log(string message)
    {
        if (!VerboseEnabled) return;
        Write(message);
    }

    /// <summary>
    /// DR1-3 : information de diagnostic de terrain, TOUJOURS ecrite quel que
    /// soit VerboseEnabled. Reservee aux traces peu volumineuses et bornees
    /// (ex. resolution de texture : une ligne par asset et par session, plus
    /// une synthese par requete GetAllMaterials) — le tout-venant par requete
    /// reste dans Log (verbose).
    /// </summary>
    public static void Info(string message)
    {
        Write(message);
    }

    /// <summary>
    /// Erreur : TOUJOURS ecrite, quel que soit VerboseEnabled (FIA-11).
    /// </summary>
    public static void Error(string message, Exception? ex = null)
    {
        Write($"ERROR: {message}");
        if (ex != null)
            Write($"  Exception: {ex.GetType().Name}: {ex.Message}\n  StackTrace: {ex.StackTrace}");
    }

    private static void Write(string message)
    {
        lock (_lock)
        {
            try
            {
                File.AppendAllText(_logPath, $"[{DateTime.Now:HH:mm:ss.fff}] {message}\n");
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    public static string LogPath => _logPath;
}
