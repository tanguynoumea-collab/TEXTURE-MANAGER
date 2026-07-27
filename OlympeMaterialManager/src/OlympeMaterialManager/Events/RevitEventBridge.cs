using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;

namespace Olympe.MaterialManager.Events;

/// <summary>
/// Handler ExternalEvent avec dispatch par enum (D-09, D-10).
/// Point de passage unique entre les ViewModels et l'API Revit.
/// Thread-safe : les requetes sont empilees dans une ConcurrentQueue depuis le
/// thread UI, drainees sur le thread Revit dans Execute(), et les callbacks sont
/// marshalles vers le thread UI WPF via Dispatcher.BeginInvoke.
/// Classe partielle (ARC-02/MAINT-03), decoupee par domaine :
/// - RevitEventBridge.cs : dispatch + infrastructure (queue, marshalling, helpers)
/// - RevitEventBridge.Queries.cs : handlers de lecture (couches, parametres, materiaux)
/// - RevitEventBridge.Materials.cs : handlers d'ecriture (Set/Duplicate/Edit, transactions)
/// - RevitEventBridge.Pick.cs : selection 3D (pick, surbrillance)
/// </summary>
public partial class RevitEventBridge : IExternalEventHandler
{
    private record struct RequestEntry(RevitRequestType Type, object? Data, Action<object?> Callback);
    private readonly System.Collections.Concurrent.ConcurrentQueue<RequestEntry> _queue = new();

    /// <summary>
    /// Envoie une requete au thread Revit via ExternalEvent.
    /// Utilise une file d'attente pour supporter plusieurs requetes simultanees.
    /// FIA-04 : si Raise() ne retourne ni Accepted ni Pending, la requete ne sera
    /// jamais executee — elle est retiree de la queue et le callback recoit une
    /// exception pour que les flags d'occupation (busy/pick) se liberent.
    /// </summary>
    public void MakeRequest(RevitRequestType type, object? data, Action<object?> callback)
    {
        LogService.Log($"MakeRequest: {type}, data={data?.GetType().Name ?? "null"}");
        var entry = new RequestEntry(type, data, callback);
        _queue.Enqueue(entry);
        var raiseResult = App.RevitEvent.Raise();
        LogService.Log($"MakeRequest: Raise() returned {raiseResult}, queue size={_queue.Count}");

        if (raiseResult != ExternalEventRequest.Accepted && raiseResult != ExternalEventRequest.Pending)
        {
            RemoveFromQueue(entry);
            LogService.Error($"MakeRequest: Raise() refuse ({raiseResult}) pour {type}, requete abandonnee");
            callback(new InvalidOperationException(
                $"Revit n'a pas accepte la requete (etat : {raiseResult})."));
        }
    }

    /// <summary>
    /// Retire une entree precise de la queue (drain + re-enqueue des autres, FIA-04).
    /// Appele uniquement depuis le thread UI quand Raise() vient d'echouer.
    /// </summary>
    private void RemoveFromQueue(RequestEntry entry)
    {
        var kept = new List<RequestEntry>();
        bool removed = false;
        while (_queue.TryDequeue(out var e))
        {
            if (!removed && e.Equals(entry))
            {
                removed = true;
                continue;
            }
            kept.Add(e);
        }
        foreach (var e in kept)
            _queue.Enqueue(e);
    }

    /// <summary>
    /// IExternalEventHandler.Execute — appele par Revit quand l'ExternalEvent est leve.
    /// Traite TOUTES les requetes en attente dans la queue.
    /// </summary>
    public void Execute(UIApplication app)
    {
        LogService.Log($"Execute() called by Revit, queue size={_queue.Count}");
        while (_queue.TryDequeue(out var entry))
        {
            ProcessSingleRequest(app, entry.Type, entry.Data, entry.Callback);
        }
    }

    /// <summary>
    /// IExternalEventHandler.GetName — nom affiche dans Revit.
    /// </summary>
    public string GetName() => "Olympe MaterialManager Bridge";

    /// <summary>
    /// Retourne le document actif ou leve une InvalidOperationException avec un
    /// message francais si aucun document n'est ouvert (FIA-06). Utilise par les
    /// handlers d'ecriture a la place de ActiveUIDocument! (NRE technique).
    /// </summary>
    private static Document GetActiveDocument(UIApplication uiApp)
    {
        var doc = uiApp.ActiveUIDocument?.Document;
        if (doc == null)
            throw new InvalidOperationException("Aucun document actif.");
        return doc;
    }

    /// <summary>
    /// Traite une seule requete sur le thread Revit.
    /// </summary>
    private void ProcessSingleRequest(UIApplication uiApp, RevitRequestType type, object? data, Action<object?> callback)
    {
        if (type == RevitRequestType.None)
        {
            LogService.Log("ProcessSingleRequest: skipped (type=None)");
            return;
        }

        LogService.Log($"ProcessSingleRequest: dispatching {type}");
        object? result = null;
        try
        {
            switch (type)
            {
                case RevitRequestType.GetLayersForType:
                    result = HandleGetLayersForType(uiApp, (long)data!);
                    break;
                case RevitRequestType.GetMaterialParametersForType:
                    result = HandleGetMaterialParametersForType(uiApp, (long)data!);
                    break;

                // Phase 3 : preset panel et Set Mat
                case RevitRequestType.GetAllMaterials:
                    result = HandleGetAllMaterials(uiApp);
                    break;
                case RevitRequestType.SetMaterialOnLayers:
                    HandleSetMaterialOnLayers(uiApp, (SetMatRequestDto)data!);
                    break;
                case RevitRequestType.SetMaterialOnParameter:
                    HandleSetMaterialOnParameter(uiApp, (SetMatParamRequestDto)data!);
                    break;
                case RevitRequestType.DuplicateMaterial:
                    result = HandleDuplicateMaterial(uiApp, (DuplicateMaterialRequestDto)data!);
                    break;

                // Phase 4 : edition materiau et pick 3D
                case RevitRequestType.GetMaterialDetails:
                    result = HandleGetMaterialDetails(uiApp, (long)data!);
                    break;
                case RevitRequestType.EditMaterialName:
                    HandleEditMaterialName(uiApp, (EditMaterialNameRequestDto)data!);
                    break;
                case RevitRequestType.EditMaterialDescription:
                    HandleEditMaterialDescription(uiApp, (EditMaterialDescriptionRequestDto)data!);
                    break;
                case RevitRequestType.EditMaterialColor:
                    HandleEditMaterialColor(uiApp, (EditMaterialColorRequestDto)data!);
                    break;
                case RevitRequestType.EditMaterialTint:
                    HandleEditMaterialTint(uiApp, (EditMaterialTintRequestDto)data!);
                    break;
                case RevitRequestType.PickElementInView:
                    result = HandlePickElementInView(uiApp);
                    break;
                case RevitRequestType.HighlightElementsByType:
                    HandleHighlightElementsByType(uiApp, (long)data!);
                    break;
                case RevitRequestType.GetCompositeSubTypes:
                    result = HandleGetCompositeSubTypes(uiApp, (long)data!);
                    break;
            }
        }
        catch (Exception ex)
        {
            LogService.Error($"ProcessRequest: handler {type} threw", ex);
            result = ex;
        }

        LogService.Log($"ProcessRequest: {type} done, result={result?.GetType().Name ?? "null"}");

        // Marshaller le resultat vers le thread UI WPF (BeginInvoke pour eviter deadlock)
        try
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher != null)
            {
                LogService.Log($"ProcessRequest: BeginInvoke callback for {type}");
                dispatcher.BeginInvoke(new Action(() =>
                {
                    LogService.Log($"Callback: executing for {type}");
                    try
                    {
                        callback(result);
                        LogService.Log($"Callback: completed for {type}");
                    }
                    catch (Exception cbEx)
                    {
                        LogService.Error($"Callback: failed for {type}", cbEx);
                    }
                }));
            }
            else
            {
                LogService.Log($"ProcessRequest: Dispatcher is null! Calling callback directly for {type}");
                callback(result);
            }
        }
        catch (Exception dispEx)
        {
            LogService.Error($"ProcessRequest: Dispatcher.BeginInvoke failed for {type}", dispEx);
            // FIA-08 : le callback de secours ne doit pas echouer silencieusement.
            try { callback(result); }
            catch (Exception cbEx) { LogService.Error($"ProcessRequest: fallback callback failed for {type}", cbEx); }
        }
    }
}
