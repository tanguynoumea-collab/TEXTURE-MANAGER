using System.Collections;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Messages;
using Olympe.MaterialManager.Models;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel du panneau central (Couches / Parametres).
/// Recoit TypeSelectedMessage du LeftPanel, fetch les couches ou parametres materiaux
/// via RevitEventBridge, et expose les donnees pour le binding XAML.
/// Multi-selection supportee via SelectedItems (D-18).
/// </summary>
public partial class CenterPanelViewModel : ObservableObject
{
    private readonly RevitEventBridge? _eventBridge;

    [ObservableProperty]
    private string _panelTitle = "Couches / Paramètres";

    [ObservableProperty]
    private ObservableCollection<LayerDto> _layers = new();

    [ObservableProperty]
    private ObservableCollection<MaterialParamDto> _materialParams = new();

    [ObservableProperty]
    private bool _showLayers;

    [ObservableProperty]
    private bool _showParameters;

    [ObservableProperty]
    private bool _showPlaceholder = true;

    [ObservableProperty]
    private string _selectedTypeName = string.Empty;

    [ObservableProperty]
    private string _modeLabel = string.Empty;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _errorMessage = string.Empty;

    /// <summary>
    /// Indique qu'un type composite est selectionne (pas un sous-type).
    /// Affiche un message invitant a selectionner un sous-type.
    /// </summary>
    [ObservableProperty]
    private bool _showCompositeMessage;

    [ObservableProperty]
    private IList? _selectedItems;

    [ObservableProperty]
    private long _currentTypeIdValue;

    /// <summary>
    /// Constructeur principal avec injection du bridge ExternalEvent.
    /// Enregistre la reception de TypeSelectedMessage (D-19, D-20) et RefreshLayersMessage (D-25).
    /// </summary>
    public CenterPanelViewModel(RevitEventBridge eventBridge)
    {
        _eventBridge = eventBridge;

        WeakReferenceMessenger.Default.Register<TypeSelectedMessage>(this, (r, m) =>
        {
            ((CenterPanelViewModel)r).OnTypeSelected(m.Value);
        });

        WeakReferenceMessenger.Default.Register<RefreshLayersMessage>(this, (r, m) =>
        {
            var vm = (CenterPanelViewModel)r;
            if (vm.ShowLayers)
                vm.FetchLayers(m.Value);
            else if (vm.ShowParameters)
                vm.FetchMaterialParameters(m.Value);
        });
    }

    /// <summary>
    /// Constructeur sans parametre pour le designer WPF.
    /// </summary>
    public CenterPanelViewModel() : this(null!)
    {
    }

    /// <summary>
    /// Reagit a la selection d'un type dans le TreeView du panneau gauche.
    /// Dispatch vers FetchLayers ou FetchMaterialParameters selon HasCompoundStructure.
    /// </summary>
    private void OnTypeSelected(SceneTypeDto? type)
    {
        Layers.Clear();
        MaterialParams.Clear();
        ErrorMessage = string.Empty;
        SelectedItems = null;
        ShowCompositeMessage = false;

        if (type == null)
        {
            ShowLayers = false;
            ShowParameters = false;
            ShowPlaceholder = true;
            SelectedTypeName = string.Empty;
            ModeLabel = string.Empty;
            CurrentTypeIdValue = 0;
            return;
        }

        ShowPlaceholder = false;
        SelectedTypeName = $"{type.FamilyName} : {type.TypeName}";
        CurrentTypeIdValue = type.ElementIdValue;

        // Type composite : afficher un message invitant a selectionner un sous-type
        if (type.IsComposite)
        {
            ShowLayers = false;
            ShowParameters = false;
            ShowCompositeMessage = true;
            ModeLabel = "Type composé";
            return;
        }

        if (type.HasCompoundStructure)
        {
            FetchLayers(type.ElementIdValue);
        }
        else
        {
            FetchMaterialParameters(type.ElementIdValue);
        }
    }

    /// <summary>
    /// Recupere les couches CompoundStructure via RevitEventBridge (LAYER-01, LAYER-02).
    /// </summary>
    private void FetchLayers(long typeIdValue)
    {
        IsLoading = true;
        ModeLabel = "Couches";
        ShowLayers = true;
        ShowParameters = false;

        _eventBridge?.MakeRequest(RevitRequestType.GetLayersForType, typeIdValue, result =>
        {
            IsLoading = false;
            if (result is List<LayerDto> layers)
            {
                Layers.Clear();
                foreach (var layer in layers)
                    Layers.Add(layer);
            }
            else if (result is Exception ex)
            {
                ErrorMessage = $"Erreur : {ex.Message}";
            }
        });
    }

    /// <summary>
    /// Recupere les parametres materiaux via RevitEventBridge (LAYER-03).
    /// </summary>
    private void FetchMaterialParameters(long typeIdValue)
    {
        IsLoading = true;
        ModeLabel = "Paramètres matériaux";
        ShowParameters = true;
        ShowLayers = false;

        _eventBridge?.MakeRequest(RevitRequestType.GetMaterialParametersForType, typeIdValue, result =>
        {
            IsLoading = false;
            if (result is List<MaterialParamDto> matParams)
            {
                MaterialParams.Clear();
                foreach (var param in matParams)
                    MaterialParams.Add(param);
            }
            else if (result is Exception ex)
            {
                ErrorMessage = $"Erreur : {ex.Message}";
            }
        });
    }

    /// <summary>
    /// Commande invoquee par le EventTrigger SelectionChanged du ListBox.
    /// Met a jour SelectedItems pour usage downstream (Set Mat, D-18).
    /// </summary>
    [RelayCommand]
    private void SelectionChanged(object? parameter)
    {
        SelectedItems = parameter as IList;
    }
}
