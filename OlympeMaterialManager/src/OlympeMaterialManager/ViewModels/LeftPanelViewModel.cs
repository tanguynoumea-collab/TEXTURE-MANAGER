using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel du panneau gauche : gestion des scenes, TreeView avec types groupes par categorie,
/// ComboBoxes d'ajout de types, selection avec notification Messenger (SCENE-01 a SCENE-08).
/// </summary>
public partial class LeftPanelViewModel : ObservableObject
{
    private readonly RevitEventBridge? _eventBridge;

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
    /// Nom saisi pour la creation d'une nouvelle scene (D-03).
    /// </summary>
    [ObservableProperty]
    private string _newSceneName = string.Empty;

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
    public LeftPanelViewModel(RevitEventBridge eventBridge)
    {
        _eventBridge = eventBridge;
    }

    /// <summary>
    /// Constructeur sans parametre pour le designer WPF.
    /// </summary>
    public LeftPanelViewModel() : this(null!)
    {
    }

    /// <summary>
    /// Cree une nouvelle scene et l'ajoute a la collection (SCENE-01, D-03).
    /// </summary>
    [RelayCommand]
    private void CreerScene()
    {
        if (string.IsNullOrWhiteSpace(NewSceneName))
            return;

        var scene = new SceneDto { Name = NewSceneName.Trim() };
        Scenes.Add(scene);
        ActiveScene = scene;
        NewSceneName = string.Empty;
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

            if (result is SceneTypeDto pickedType)
            {
                // D-12: Add to active scene (with duplicate check by ElementIdValue)
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
                }
            }
            else if (result is Exception ex)
            {
                // D-14: View3D validation error or other error
                ErrorMessage = ex.Message;
            }
            // result == null means user pressed Escape (D-13): no action needed
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
