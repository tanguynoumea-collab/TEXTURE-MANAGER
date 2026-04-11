using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows.Data;
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
/// ComboBoxes d'ajout de types, selection avec notification Messenger (SCENE-01 a SCENE-08).
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
    /// Liste des familles disponibles, peuplee par GetFamilyList.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<FamilyCategoryDto> _families = new();

    /// <summary>
    /// Famille selectionnee dans le premier ComboBox.
    /// </summary>
    [ObservableProperty]
    private FamilyCategoryDto? _selectedFamily;

    /// <summary>
    /// Types de la famille selectionnee, peuples par GetTypeList.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<SceneTypeDto> _familyTypes = new();

    /// <summary>
    /// Type selectionne dans le second ComboBox (pour ajout a la scene).
    /// </summary>
    [ObservableProperty]
    private SceneTypeDto? _selectedFamilyType;

    /// <summary>
    /// Type selectionne dans le TreeView. Envoie TypeSelectedMessage via Messenger.
    /// </summary>
    [ObservableProperty]
    private SceneTypeDto? _selectedType;

    /// <summary>
    /// Indique le chargement des familles en cours.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingFamilies;

    /// <summary>
    /// Indique le chargement des types en cours.
    /// </summary>
    [ObservableProperty]
    private bool _isLoadingTypes;

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
        var collection = _presetService.LoadScenes();
        foreach (var scene in collection.Scenes)
            Scenes.Add(scene);
        if (Scenes.Count > 0)
            ActiveScene = Scenes[0];
    }

    /// <summary>
    /// Sauvegarde toutes les scenes dans le fichier JSON.
    /// </summary>
    private void AutoSaveScenes()
    {
        if (_presetService == null) return;
        var collection = new SceneCollectionDto { Scenes = Scenes };
        _presetService.SaveScenes(collection);
    }

    /// <summary>
    /// Cree une nouvelle scene via un dialog de saisie (SCENE-01, D-03).
    /// </summary>
    [RelayCommand]
    private void CreerScene()
    {
        var dialog = new CreateNameDialog();
        dialog.Title = "Nouvelle scene";
        dialog.SetPrompt("Nom de la scene :");
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
            Title = "Charger une scene depuis un fichier externe",
            Filter = "Fichiers JSON (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() != true || _presetService == null) return;

        try
        {
            var sourcePath = dialog.FileName;
            var scenesPath = _presetService.GetScenesPath();
            var destDir = Path.GetDirectoryName(scenesPath)!;
            Directory.CreateDirectory(destDir);

            // Lire le contenu du fichier externe
            var json = File.ReadAllText(sourcePath);
            var importedCollection = System.Text.Json.JsonSerializer.Deserialize<SceneCollectionDto>(json,
                new System.Text.Json.JsonSerializerOptions { PropertyNamingPolicy = System.Text.Json.JsonNamingPolicy.CamelCase });

            if (importedCollection?.Scenes != null)
            {
                // Ajouter les scenes importees a la collection existante
                foreach (var scene in importedCollection.Scenes)
                {
                    Scenes.Add(scene);
                }

                if (importedCollection.Scenes.Count > 0)
                    ActiveScene = importedCollection.Scenes[importedCollection.Scenes.Count - 1];

                AutoSaveScenes();

                // Copier le fichier source dans le dossier local pour reference
                var destPath = Path.Combine(destDir, Path.GetFileName(sourcePath));
                if (destPath != sourcePath)
                    File.Copy(sourcePath, destPath, overwrite: true);
            }
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Erreur chargement scene externe : {ex.Message}";
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
        AutoSaveScenes();
    }

    private bool CanSupprimerType() => SelectedType != null && ActiveScene != null;

    /// <summary>
    /// Ajoute le type selectionne dans le ComboBox a la scene active (SCENE-03, D-09).
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAjouterType))]
    private void AjouterType()
    {
        if (SelectedFamilyType == null || ActiveScene == null)
            return;

        // Eviter les doublons par ElementIdValue
        foreach (var existing in ActiveScene.Types)
        {
            if (existing.ElementIdValue == SelectedFamilyType.ElementIdValue)
                return;
        }

        ActiveScene.Types.Add(SelectedFamilyType);
        AutoSaveScenes();
    }

    private bool CanAjouterType() => SelectedFamilyType != null && ActiveScene != null;

    /// <summary>
    /// Ajoute un type a la scene active via clic dans la vue 3D (D-11, D-12, D-13, D-14, SCENE-04, SCENE-09).
    /// Le handler RevitEventBridge gere : validation View3D, hide/show fenetre, PickObject.
    /// </summary>
    [RelayCommand(CanExecute = nameof(CanAjouterParClic))]
    private void AjouterParClic()
    {
        if (_eventBridge == null || ActiveScene == null) return;

        IsPickMode = true;
        ErrorMessage = string.Empty;

        _eventBridge.MakeRequest(RevitRequestType.PickElementInView, null, result =>
        {
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
                    }
                }

                if (added > 0)
                {
                    SetupCustomSort();
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
    /// Charge la liste des familles depuis Revit via ExternalEvent.
    /// </summary>
    [RelayCommand]
    private void ChargerFamilles()
    {
        IsLoadingFamilies = true;
        ErrorMessage = string.Empty;

        _eventBridge?.MakeRequest(RevitRequestType.GetFamilyList, null, result =>
        {
            try
            {
                if (result is List<FamilyCategoryDto> list)
                {
                    Families.Clear();
                    foreach (var item in list)
                        Families.Add(item);
                }
                else if (result is Exception ex)
                {
                    ErrorMessage = "Erreur : " + ex.Message;
                }
            }
            finally
            {
                IsLoadingFamilies = false;
            }
        });
    }

    /// <summary>
    /// Commande pour gerer la selection dans le TreeView via EventTrigger (Pitfall 4).
    /// </summary>
    [RelayCommand]
    private void TreeViewSelectionChanged(object? parameter)
    {
        SelectedType = parameter as SceneTypeDto;
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

    // --- Partial methods for property change hooks ---

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
            ChargerFamillesCommand.Execute(null);
            SetupCustomSort();
        }

        // Notify CanExecute changes
        SupprimerTypeCommand.NotifyCanExecuteChanged();
        AjouterTypeCommand.NotifyCanExecuteChanged();
        AjouterParClicCommand.NotifyCanExecuteChanged();
    }

    partial void OnSelectedFamilyChanged(FamilyCategoryDto? value)
    {
        FamilyTypes.Clear();
        SelectedFamilyType = null;

        if (value == null)
            return;

        IsLoadingTypes = true;
        ErrorMessage = string.Empty;

        var requestDto = new GetTypeListRequestDto
        {
            FamilyElementIdValue = value.FamilyElementIdValue,
            IsSystemFamily = value.IsSystemFamily,
            BuiltInCategoryValue = value.BuiltInCategoryValue
        };

        _eventBridge?.MakeRequest(RevitRequestType.GetTypeList, requestDto, result =>
        {
            try
            {
                if (result is List<SceneTypeDto> list)
                {
                    FamilyTypes.Clear();
                    foreach (var item in list)
                        FamilyTypes.Add(item);
                }
                else if (result is Exception ex)
                {
                    ErrorMessage = "Erreur : " + ex.Message;
                }
            }
            finally
            {
                IsLoadingTypes = false;
            }
        });
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

    partial void OnSelectedFamilyTypeChanged(SceneTypeDto? value)
    {
        AjouterTypeCommand.NotifyCanExecuteChanged();
    }

    partial void OnIsPickModeChanged(bool value)
    {
        AjouterParClicCommand.NotifyCanExecuteChanged();
    }
}
