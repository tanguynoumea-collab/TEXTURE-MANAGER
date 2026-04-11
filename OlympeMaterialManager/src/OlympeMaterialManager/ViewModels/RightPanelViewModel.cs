using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Messages;
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
    /// Groupe actuellement selectionne dans le TreeView (pour ajout direct de materiau).
    /// </summary>
    [ObservableProperty]
    private PresetGroupDto? _selectedGroup;

    /// <summary>
    /// Sub-ViewModel pour la section editeur de materiau (MATEDIT-01 a MATEDIT-08).
    /// </summary>
    public MaterialEditorViewModel MaterialEditorVM { get; }

    /// <summary>
    /// Constructeur principal avec injection du bridge et du service de presets.
    /// </summary>
    public RightPanelViewModel(RevitEventBridge eventBridge, PresetService presetService)
    {
        _eventBridge = eventBridge;
        _presetService = presetService;
        MaterialEditorVM = new MaterialEditorViewModel(eventBridge);
        LoadPresets();

        // Ecouter les editions de materiau pour mettre a jour les noms dans les presets (D-21)
        WeakReferenceMessenger.Default.Register<MaterialEditedMessage>(this, (_, msg) => OnMaterialEdited(msg));
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
    /// Ajoute un materiau du projet au groupe actuellement selectionne (D-10).
    /// Si aucun groupe n'est selectionne, affiche le dialog complet avec choix de groupe.
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

        // Determiner le groupe cible (selectionne ou premier disponible)
        var targetGroup = SelectedGroup ?? (PresetGroups.Count > 0 ? PresetGroups[0] : null);

        if (targetGroup == null)
        {
            StatusMessage = "Creez d'abord un groupe de presets.";
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

                // Pre-selectionner le groupe cible dans le dialog
                dialog.PreselectGroup(targetGroup);

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
    /// Deplace un materiau d'un groupe source vers un groupe cible (drag and drop).
    /// </summary>
    public void MoveMaterial(PresetMaterialDto material, PresetGroupDto sourceGroup, PresetGroupDto targetGroup)
    {
        if (sourceGroup == targetGroup) return;

        sourceGroup.Materials.Remove(material);
        targetGroup.Materials.Add(material);
        StatusMessage = $"\"{material.MaterialName}\" deplace vers \"{targetGroup.GroupName}\".";
        AutoSave();
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
    /// Met a jour SelectedPresetMaterial et SelectedGroup quand la selection TreeView change.
    /// </summary>
    [RelayCommand]
    private void TreeViewSelectionChanged(object? param)
    {
        SelectedPresetMaterial = param as PresetMaterialDto;

        // Tracker aussi le groupe selectionne
        if (param is PresetGroupDto group)
        {
            SelectedGroup = group;
        }
        else if (param is PresetMaterialDto mat)
        {
            SelectedGroup = FindGroupContaining(mat);
        }
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
    /// Envoie MaterialSelectedMessage quand la selection du preset change (D-20).
    /// </summary>
    partial void OnSelectedPresetMaterialChanged(PresetMaterialDto? value)
    {
        WeakReferenceMessenger.Default.Send(new MaterialSelectedMessage(value));
    }

    /// <summary>
    /// Met a jour le nom du materiau dans les presets apres edition (D-21).
    /// </summary>
    private void OnMaterialEdited(MaterialEditedMessage msg)
    {
        foreach (var group in PresetGroups)
        {
            foreach (var mat in group.Materials)
            {
                if (mat.MaterialElementIdValue == msg.Value)
                {
                    // Rafraichir le nom et la couleur depuis le sub-VM
                    mat.MaterialName = MaterialEditorVM.MaterialName;
                    mat.ColorArgb = MaterialEditorVM.ColorArgb;
                }
            }
        }
        AutoSave();
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
