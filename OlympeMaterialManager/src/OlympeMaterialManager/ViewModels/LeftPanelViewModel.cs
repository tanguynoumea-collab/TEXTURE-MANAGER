using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Messages;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;
using Olympe.MaterialManager.Views;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel du panneau gauche : gestion des scenes, TreeView avec types groupes par categorie,
/// ajout de types par clic 3D, selection avec notification Messenger (SCENE-01 a SCENE-08).
/// </summary>
public partial class LeftPanelViewModel : ObservableObject
{
    private readonly RevitEventBridge? _eventBridge;
    private readonly PresetService? _presetService;

    [ObservableProperty]
    private string _panelTitle = "Familles / Types";

    /// <summary>
    /// Collection de toutes les scenes creees par l'utilisateur (D-02).
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SceneDto> _scenes = new();

    /// <summary>
    /// Scene actuellement selectionnee dans le ComboBox (D-02).
    /// </summary>
    [ObservableProperty]
    private SceneDto? _activeScene;

    /// <summary>
    /// Type selectionne dans le TreeView. Envoie TypeSelectedMessage via Messenger.
    /// </summary>
    [ObservableProperty]
    private SceneTypeDto? _selectedType;

    /// <summary>
    /// Message d'erreur affiche dans le panneau.
    /// </summary>
    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Indique que le mode pick 3D est actif (D-11, SCENE-04).
    /// </summary>
    [ObservableProperty]
    private bool _isPickMode;

    /// <summary>
    /// Tooltip du bouton Ajouter par clic.
    /// </summary>
    [ObservableProperty]
    private string _pickButtonTooltip = "Ajouter un type par clic dans la vue 3D";

    /// <summary>
    /// Texte de recherche du panneau (B5-G). Applique avec un debounce de ~200 ms
    /// via _searchDebounceTimer pour ne pas re-filtrer a chaque frappe.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// True quand la recherche est active et qu'aucun type ne matche (pattern UI-m14).
    /// </summary>
    [ObservableProperty]
    private bool _isSearchNoResult;

    /// <summary>
    /// Timer de debounce de la recherche (B5-G). Cree paresseusement sur le thread UI.
    /// </summary>
    private DispatcherTimer? _searchDebounceTimer;

    // Note UI-M7 : les identifiants de code restent sans accents ; seules les chaines
    // visibles par l'utilisateur sont accentuees.

    /// <summary>
    /// Types de la scene active, utilise pour le binding du TreeView.
    /// Non genere par [ObservableProperty] -- leve PropertyChanged manuellement.
    /// </summary>
    public ObservableCollection<SceneTypeDto>? ActiveSceneTypes => ActiveScene?.Types;

    /// <summary>
    /// Constructeur principal avec injection du bridge ExternalEvent.
    /// </summary>
    public LeftPanelViewModel(RevitEventBridge eventBridge, PresetService presetService)
    {
        _eventBridge = eventBridge;
        _presetService = presetService;
        LoadScenes();
    }

    /// <summary>
    /// Constructeur sans parametre pour le designer WPF.
    /// </summary>
    public LeftPanelViewModel() : this(null!, null!)
    {
    }

    /// <summary>
    /// Charge les scenes sauvegardees depuis le fichier JSON.
    /// </summary>
    private void LoadScenes()
    {
        if (_presetService == null) return;
        var collection = _presetService.LoadScenes(out var loadFailed);
        _scenesLoadFailed = loadFailed;
        foreach (var scene in collection.Scenes)
            Scenes.Add(scene);
        if (Scenes.Count > 0)
            ActiveScene = Scenes[0];

        if (loadFailed)
        {
            ErrorMessage = "Une ou plusieurs scènes sont illisibles : fichiers mis de côté (.corrupt), sauvegarde automatique des scènes désactivée.";
        }
    }

    /// <summary>
    /// True si le dernier chargement des scenes a rencontre un fichier illisible (DON-02).
    /// Tant que ce flag est leve, l'AutoSave des scenes est bloque pour ne pas ecraser de donnees.
    /// </summary>
    private bool _scenesLoadFailed;

    /// <summary>
    /// Sauvegarde toutes les scenes dans le fichier JSON.
    /// </summary>
    private void AutoSaveScenes()
    {
        if (_presetService == null) return;
        // Chargement en echec : ne surtout pas ecraser les fichiers (DON-02)
        if (_scenesLoadFailed) return;

        // FIA-03 : ecriture declenchee depuis le thread UI du process Revit —
        // toute exception I/O doit etre signalee, jamais propagee a l'hote.
        try
        {
            var collection = new SceneCollectionDto { Scenes = Scenes };
            _presetService.SaveScenes(collection);
        }
        catch (Exception ex)
        {
            LogService.Error("Echec de sauvegarde des scenes", ex);
            ErrorMessage = $"Échec de sauvegarde des scènes : {ex.Message}";
        }
    }

    /// <summary>
    /// Supprime la scene active et son fichier JSON.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSupprimerScene))]
    private void SupprimerScene()
    {
        if (ActiveScene == null || _presetService == null) return;

        if (!DialogService.Confirm(
                $"Supprimer la scène \"{ActiveScene.Name}\" ?",
                "Supprimer la scène"))
            return;

        var name = ActiveScene.Name;
        Scenes.Remove(ActiveScene);
        _presetService.DeleteScene(name);
        ActiveScene = Scenes.Count > 0 ? Scenes[0] : null;
    }

    private bool CanSupprimerScene() => ActiveScene != null;

    /// <summary>
    /// Cree une nouvelle scene via un dialog de saisie (SCENE-01, D-03).
    /// </summary>
    [RelayCommand]
    private void CreerScene()
    {
        var dialog = new CreateNameDialog();
        dialog.Title = "Nouvelle scène";
        dialog.SetPrompt("Nom de la scène :");
        dialog.Owner = App.MainWindow;

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.EnteredName))
        {
            var scene = new SceneDto { Name = dialog.EnteredName };
            Scenes.Add(scene);
            ActiveScene = scene;
            AutoSaveScenes();
        }
    }

    /// <summary>
    /// Charge un fichier scene externe via OpenFileDialog.
    /// Copie le fichier dans le dossier scenes pour la persistence, puis charge les scenes.
    /// </summary>
    [RelayCommand]
    private void ChargerSceneExterne()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Charger une scène depuis un fichier externe",
            Filter = "Fichiers JSON (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() != true || _presetService == null) return;

        try
        {
            var sourcePath = dialog.FileName;
            var destDir = _presetService.GetScenesDirectory();
            var sceneName = Path.GetFileNameWithoutExtension(sourcePath);
            var destPath = Path.Combine(destDir, sceneName + ".json");

            // DON-09 : valider le JSON AVANT toute copie dans le dossier projet
            var json = File.ReadAllText(sourcePath);
            if (!PresetService.IsValidSceneJson(json))
            {
                ErrorMessage = $"Fichier invalide : \"{Path.GetFileName(sourcePath)}\" n'est pas une scène JSON lisible. Import abandonné.";
                return;
            }

            // DON-09 : collision avec une scene existante -> confirmation avant ecrasement
            if (File.Exists(destPath) && !DialogService.Confirm(
                    $"Une scène nommée \"{sceneName}\" existe déjà.\nL'écraser avec le fichier importé ?",
                    "Scène existante"))
            {
                return;
            }

            // Copier le fichier dans le dossier scenes
            File.Copy(sourcePath, destPath, overwrite: true);

            // Charger la scene depuis le fichier copie
            var scene = _presetService.LoadScene(sceneName);
            if (scene == null)
            {
                // Fichier illisible : quarantaine faite cote service (DON-02)
                ErrorMessage = $"Scène \"{sceneName}\" illisible : fichier mis de côté (.corrupt).";
                return;
            }
            // SEC-01 : le nom de la scene est TOUJOURS le nom du fichier importe,
            // jamais le champ Name du JSON externe (empeche l'injection d'un nom
            // arbitraire qui deviendrait un nom de fichier a la prochaine sauvegarde).
            scene.Name = sceneName;

            Scenes.Add(scene);
            ActiveScene = scene;
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur chargement scène externe : {ex.Message}";
        }
    }

    /// <summary>
    /// Supprime le type selectionne de la scene active (SCENE-05, D-10).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanSupprimerType))]
    private void SupprimerType()
    {
        if (SelectedType == null || ActiveScene == null)
            return;

        ActiveScene.Types.Remove(SelectedType);
        SelectedType = null;
        // B5-G : l'etat « aucun resultat » peut changer apres un retrait
        ApplySearchFilter();
        AutoSaveScenes();
    }

    private bool CanSupprimerType() => SelectedType != null && ActiveScene != null;

    /// <summary>
    /// Ajoute un type a la scene active via clic dans la vue 3D (D-11, D-12, D-13, D-14, SCENE-04, SCENE-09).
    /// Le handler RevitEventBridge gere : validation View3D, PickObject.
    /// ARC-05 : le hide/show de la fenetre est gere ici, autour de la requete —
    /// le bridge ne touche jamais a la fenetre WPF.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAjouterParClic))]
    private void AjouterParClic()
    {
        if (_eventBridge == null || ActiveScene == null) return;

        IsPickMode = true;
        ErrorMessage = string.Empty;

        // ARC-05 : cacher la fenetre avant le pick, la re-afficher dans le callback.
        WindowService.HideMainWindow();

        _eventBridge.MakeRequest(RevitRequestType.PickElementInView, null, result =>
        {
            WindowService.ShowMainWindow();
            IsPickMode = false;

            if (result is List<SceneTypeDto> pickedTypes)
            {
                // Multi-selection : ajouter tous les types pickes (avec detection doublons)
                int added = 0;
                foreach (var pickedType in pickedTypes)
                {
                    bool isDuplicate = false;
                    foreach (var existing in ActiveScene!.Types)
                    {
                        if (existing.ElementIdValue == pickedType.ElementIdValue)
                        {
                            isDuplicate = true;
                            break;
                        }
                    }

                    if (!isDuplicate)
                    {
                        ActiveScene.Types.Add(pickedType);
                        added++;

                        // Pour les types composites, charger les sous-types
                        if (pickedType.IsComposite)
                        {
                            FetchCompositeSubTypes(pickedType);
                        }
                    }
                }

                if (added > 0)
                {
                    SetupCustomSort();
                    // B5-G : les nouveaux types passent par le filtre courant
                    ApplySearchFilter();
                    AutoSaveScenes();
                }
            }
            else if (result is Exception ex)
            {
                // D-14: View3D validation error or other error
                ErrorMessage = ex.Message;
            }
            // result == null means user pressed Escape with no selection (D-13): no action needed
        });
    }

    private bool CanAjouterParClic() => ActiveScene != null && !IsPickMode;

    /// <summary>
    /// Commande pour gerer la selection dans le TreeView via EventTrigger (Pitfall 4).
    /// </summary>
    [RelayCommand]
    private void TreeViewSelectionChanged(object? parameter)
    {
        SelectedType = parameter as SceneTypeDto;
    }

    /// <summary>
    /// Charge les sous-types d'un type composite via RevitEventBridge.
    /// Peuple la collection SubTypes du SceneTypeDto composite.
    /// </summary>
    private void FetchCompositeSubTypes(SceneTypeDto compositeType)
    {
        if (_eventBridge == null || !compositeType.IsComposite) return;

        _eventBridge.MakeRequest(RevitRequestType.GetCompositeSubTypes, compositeType.ElementIdValue, result =>
        {
            if (result is List<SceneTypeDto> subTypes && subTypes.Count > 0)
            {
                compositeType.SubTypes = new ObservableCollection<SceneTypeDto>(subTypes);
            }
        });
    }

    /// <summary>
    /// Configure le tri personnalise et le groupement sur la CollectionView des types actifs.
    /// </summary>
    private void SetupCustomSort()
    {
        if (ActiveScene?.Types == null)
            return;

        var view = CollectionViewSource.GetDefaultView(ActiveScene.Types);
        if (view is ListCollectionView listView)
        {
            listView.CustomSort = new CategorySortComparer();
        }

        if (view.GroupDescriptions.Count == 0)
        {
            view.GroupDescriptions.Add(new PropertyGroupDescription("CategoryName"));
        }
    }

    // --- Recherche (B5-G) ---

    /// <summary>
    /// Efface la recherche (bouton ✕ et lien de l'etat « aucun resultat », UI-m14).
    /// </summary>
    [RelayCommand]
    private void EffacerRecherche()
    {
        SearchText = string.Empty;
    }

    /// <summary>
    /// Applique (ou retire) le predicat de recherche sur la CollectionView des types.
    /// Le Filter coexiste avec le tri et le groupement poses par SetupCustomSort.
    /// Appele apres le debounce, au changement de scene et apres ajout/retrait de types.
    /// </summary>
    private void ApplySearchFilter()
    {
        if (ActiveScene?.Types == null)
        {
            IsSearchNoResult = false;
            return;
        }

        var view = CollectionViewSource.GetDefaultView(ActiveScene.Types);
        if (string.IsNullOrWhiteSpace(SearchText))
        {
            if (view.Filter != null)
                view.Filter = null;
            IsSearchNoResult = false;
            return;
        }

        view.Filter = MatchesSearch;
        view.Refresh();
        IsSearchNoResult = view.IsEmpty;
    }

    /// <summary>
    /// Predicat de la CollectionView : un type reste visible si FamilyName ou TypeName
    /// matche la recherche, ou si l'un de ses sous-types composites matche.
    /// </summary>
    private bool MatchesSearch(object item)
    {
        if (item is not SceneTypeDto type) return false;

        if (SearchMatcher.Matches(type.FamilyName, SearchText) ||
            SearchMatcher.Matches(type.TypeName, SearchText))
        {
            return true;
        }

        // Sous-types d'un composite : le parent reste visible si un sous-type matche
        if (type.SubTypes != null)
        {
            foreach (var sub in type.SubTypes)
            {
                if (SearchMatcher.Matches(sub.FamilyName, SearchText) ||
                    SearchMatcher.Matches(sub.TypeName, SearchText))
                {
                    return true;
                }
            }
        }

        return false;
    }

    // --- Partial methods for property change hooks ---

    /// <summary>
    /// Debounce ~200 ms de la recherche (B5-G). L'effacement est applique
    /// immediatement pour que le bouton ✕ reagisse sans latence.
    /// </summary>
    partial void OnSearchTextChanged(string value)
    {
        if (_searchDebounceTimer == null)
        {
            _searchDebounceTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _searchDebounceTimer.Tick += (_, _) =>
            {
                _searchDebounceTimer!.Stop();
                ApplySearchFilter();
            };
        }

        _searchDebounceTimer.Stop();
        if (string.IsNullOrWhiteSpace(value))
            ApplySearchFilter();
        else
            _searchDebounceTimer.Start();
    }

    partial void OnActiveSceneChanged(SceneDto? value)
    {
        OnPropertyChanged(nameof(ActiveSceneTypes));
        SelectedType = null;

        // Mettre a jour le titre du panneau avec le nom de la scene active
        PanelTitle = value != null
            ? $"Familles / Types - {value.Name}"
            : "Familles / Types";

        if (value != null)
        {
            SetupCustomSort();

            // B5-G : re-appliquer la recherche courante sur la view de la nouvelle scene
            ApplySearchFilter();

            // Recharger les sous-types pour les types composites deja dans la scene
            foreach (var type in value.Types)
            {
                if (type.IsComposite && (type.SubTypes == null || type.SubTypes.Count == 0))
                {
                    FetchCompositeSubTypes(type);
                }
            }
        }

        // Notify CanExecute changes
        SupprimerTypeCommand.NotifyCanExecuteChanged();
        AjouterParClicCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedTypeChanged(SceneTypeDto? value)
    {
        WeakReferenceMessenger.Default.Send(new TypeSelectedMessage(value));
        SupprimerTypeCommand.NotifyCanExecuteChanged();

        // Highlight dans la vue Revit tous les elements du type selectionne
        if (value != null && _eventBridge != null)
        {
            _eventBridge.MakeRequest(RevitRequestType.HighlightElementsByType, value.ElementIdValue, _ => { });
        }
    }

    partial void OnIsPickModeChanged(bool value)
    {
        AjouterParClicCommand.NotifyCanExecuteChanged();
    }
}
