using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;
using Olympe.MaterialManager.Views;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel du panneau droit (Materiaux Preset).
/// Gere les groupes de presets, CRUD sur les materiaux, persistance JSON via PresetService.
/// </summary>
public partial class RightPanelViewModel : ObservableObject
{
    private readonly RevitEventBridge? _eventBridge;
    private readonly PresetService _presetService;

    private string? _presetFilePath;
    private PresetCollectionDto? _collection;

    [ObservableProperty]
    private string _panelTitle = "Materiaux Preset";

    [ObservableProperty]
    private ObservableCollection<PresetGroupDto> _presetGroups = new();

    [ObservableProperty]
    private PresetMaterialDto? _selectedPresetMaterial;

    [ObservableProperty]
    private string _newGroupName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Constructeur principal avec injection du bridge et du service de presets.
    /// </summary>
    public RightPanelViewModel(RevitEventBridge eventBridge, PresetService presetService)
    {
        _eventBridge = eventBridge;
        _presetService = presetService;
        LoadPresets();
    }

    /// <summary>
    /// Constructeur sans parametre pour le designer WPF.
    /// </summary>
    public RightPanelViewModel() : this(null!, new PresetService())
    {
    }

    /// <summary>
    /// Cree un nouveau groupe de presets a partir du nom saisi (D-11).
    /// </summary>
    [RelayCommand]
    private void CreerGroupe()
    {
        if (string.IsNullOrWhiteSpace(NewGroupName))
            return;

        var group = new PresetGroupDto { GroupName = NewGroupName.Trim() };
        PresetGroups.Add(group);
        _collection?.Groups.Add(group);
        NewGroupName = string.Empty;
        StatusMessage = $"Groupe \"{group.GroupName}\" cree.";
        AutoSave();
    }

    /// <summary>
    /// Ouvre un dialogue pour ajouter un materiau du projet a un groupe preset (D-10).
    /// Interroge Revit via GetAllMaterials, puis affiche AddMaterialDialog.
    /// </summary>
    [RelayCommand]
    private void AjouterMateriau()
    {
        if (_eventBridge == null)
        {
            StatusMessage = "Bridge Revit non disponible.";
            return;
        }

        _eventBridge.MakeRequest(RevitRequestType.GetAllMaterials, null, result =>
        {
            if (result is List<PresetMaterialDto> materials)
            {
                var dialog = new AddMaterialDialog
                {
                    AllMaterials = materials,
                    PresetGroups = PresetGroups
                };
                dialog.InitializeCollectionView();

                if (dialog.ShowDialog() == true &&
                    dialog.SelectedMaterial != null &&
                    dialog.SelectedGroup != null)
                {
                    dialog.SelectedGroup.Materials.Add(dialog.SelectedMaterial);
                    StatusMessage = $"\"{dialog.SelectedMaterial.MaterialName}\" ajoute a \"{dialog.SelectedGroup.GroupName}\".";
                    AutoSave();
                }
            }
            else if (result is Exception ex)
            {
                StatusMessage = $"Erreur : {ex.Message}";
            }
        });
    }

    /// <summary>
    /// Duplique un materiau via le bridge Revit (D-12, D-13).
    /// Le nouveau materiau est ajoute au meme groupe que l'original.
    /// </summary>
    [RelayCommand]
    private void DupliquerMateriau(PresetMaterialDto? material)
    {
        if (material == null || _eventBridge == null) return;

        var request = new DuplicateMaterialRequestDto
        {
            MaterialIdValue = material.MaterialElementIdValue
        };

        _eventBridge.MakeRequest(RevitRequestType.DuplicateMaterial, request, result =>
        {
            if (result is PresetMaterialDto newMat)
            {
                // Trouver le groupe contenant le materiau original
                var group = FindGroupContaining(material);
                if (group != null)
                {
                    group.Materials.Add(newMat);
                    StatusMessage = $"\"{newMat.MaterialName}\" duplique dans \"{group.GroupName}\".";
                    AutoSave();
                }
            }
            else if (result is Exception ex)
            {
                StatusMessage = $"Erreur duplication : {ex.Message}";
            }
        });
    }

    /// <summary>
    /// Supprime un materiau d'un groupe preset (D-12).
    /// </summary>
    [RelayCommand]
    private void SupprimerMateriau(PresetMaterialDto? material)
    {
        if (material == null) return;

        var group = FindGroupContaining(material);
        if (group != null)
        {
            group.Materials.Remove(material);
            StatusMessage = $"\"{material.MaterialName}\" supprime de \"{group.GroupName}\".";

            if (SelectedPresetMaterial == material)
                SelectedPresetMaterial = null;

            AutoSave();
        }
    }

    /// <summary>
    /// Met a jour SelectedPresetMaterial quand la selection TreeView change.
    /// </summary>
    [RelayCommand]
    private void TreeViewSelectionChanged(object? param)
    {
        SelectedPresetMaterial = param as PresetMaterialDto;
    }

    /// <summary>
    /// Charge les presets depuis le chemin memorise ou cree la collection par defaut (D-05).
    /// </summary>
    private void LoadPresets()
    {
        var storedPath = _presetService.GetStoredPresetPath();

        if (storedPath != null && File.Exists(storedPath))
        {
            _presetFilePath = storedPath;
            _collection = _presetService.Load(storedPath);
        }
        else
        {
            _presetFilePath = null;
            _collection = PresetService.GetDefaultCollection();
        }

        PresetGroups = _collection.Groups;
    }

    /// <summary>
    /// Sauvegarde automatique apres chaque modification (D-07).
    /// Demande un dossier a l'utilisateur si aucun chemin n'est configure.
    /// </summary>
    private void AutoSave()
    {
        if (_collection == null) return;

        if (_presetFilePath == null)
        {
            var folder = DialogService.ShowFolderBrowser("Choisir le dossier des presets");
            if (folder == null) return; // L'utilisateur a annule -- presets restent en memoire

            _presetFilePath = Path.Combine(folder, "olympe-presets.json");
            _presetService.StorePresetPath(_presetFilePath);
        }

        _presetService.Save(_collection, _presetFilePath);
    }

    /// <summary>
    /// Trouve le groupe contenant un materiau donne.
    /// </summary>
    private PresetGroupDto? FindGroupContaining(PresetMaterialDto material)
    {
        foreach (var group in PresetGroups)
        {
            if (group.Materials.Contains(material))
                return group;
        }
        return null;
    }
}
