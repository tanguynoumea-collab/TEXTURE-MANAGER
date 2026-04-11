using Autodesk.Revit.UI;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Handler ExternalEvent avec dispatch par enum (D-09, D-10).
/// Point de passage unique entre les ViewModels et l'API Revit.
/// Thread-safe : les requetes sont protegees par lock.
/// </summary>
public class RevitEventBridge
{
    private volatile RevitRequestType _requestType = RevitRequestType.None;
    private volatile object? _requestData;
    private Action<object?>? _resultCallback;
    private readonly object _lock = new();

    /// <summary>
    /// Envoie une requete au thread Revit via ExternalEvent.
    /// Appele depuis le thread UI (ViewModel).
    /// </summary>
    public void MakeRequest(RevitRequestType type, object? data, Action<object?> callback)
    {
        lock (_lock)
        {
            _requestType = type;
            _requestData = data;
            _resultCallback = callback;
        }
        App.RevitEvent.Raise();
    }

    /// <summary>
    /// Traite la requete sur le thread Revit.
    /// Appele par le callback ExternalEvent dans App.cs.
    /// </summary>
    public void ProcessRequest(UIApplication uiApp)
    {
        RevitRequestType type;
        object? data;
        Action<object?>? callback;

        lock (_lock)
        {
            type = _requestType;
            data = _requestData;
            callback = _resultCallback;
            _requestType = RevitRequestType.None;
            _requestData = null;
            _resultCallback = null;
        }

        if (type == RevitRequestType.None || callback == null)
            return;

        object? result = null;
        try
        {
            switch (type)
            {
                case RevitRequestType.GetDocumentInfo:
                    result = HandleGetDocumentInfo(uiApp);
                    break;
            }
        }
        catch (Exception ex)
        {
            result = ex;
        }

        // Marshaller le resultat vers le thread UI WPF
        System.Windows.Application.Current.Dispatcher.Invoke(() => callback(result));
    }

    private static RevitDocInfoDto HandleGetDocumentInfo(UIApplication uiApp)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        return new RevitDocInfoDto
        {
            Title = doc?.Title ?? "(aucun document)",
            PathName = doc?.PathName ?? "",
            IsValid = doc != null
        };
    }
}
