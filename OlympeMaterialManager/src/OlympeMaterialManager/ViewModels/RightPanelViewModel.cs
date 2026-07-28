using System.Collections.ObjectModel;
using System.IO;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using Olympe.MaterialManager.Events;
using Olympe.MaterialManager.Helpers;
using Olympe.MaterialManager.Messages;
using Olympe.MaterialManager.Models;
using Olympe.MaterialManager.Services;
using Olympe.MaterialManager.Views;
using Window = System.Windows.Window;

namespace Olympe.MaterialManager.ViewModels;

/// <summary>
/// ViewModel du panneau droit (Materiaux Preset).
/// Gere les groupes de presets, CRUD sur les materiaux, persistance JSON via PresetService.
/// Supporte le systeme multi-preset (chaque preset est un fichier JSON separe).
/// </summary>
public partial class RightPanelViewModel : ObservableObject
{
    private readonly RevitEventBridge? _eventBridge;
    private readonly PresetService _presetService;

    private PresetCollectionDto? _collection;

    /// <summary>
    /// True si le dernier chargement du preset actif a echoue (fichier illisible, DON-02).
    /// Tant que ce flag est leve, l'AutoSave est bloque pour ne pas ecraser de donnees.
    /// Reinitialise des qu'un chargement reussit.
    /// </summary>
    private bool _presetLoadFailed;

    [ObservableProperty]
    private string _panelTitle = "Matériaux Preset";

    [ObservableProperty]
    private ObservableCollection<PresetGroupDto> _presetGroups = new();

    [ObservableProperty]
    private PresetMaterialDto? _selectedPresetMaterial;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    /// <summary>
    /// Groupe actuellement selectionne dans le TreeView (pour ajout direct de materiau).
    /// </summary>
    [ObservableProperty]
    private PresetGroupDto? _selectedGroup;

    /// <summary>
    /// Liste des noms de presets disponibles pour le ComboBox de selection.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<string> _presetNames = new();

    /// <summary>
    /// Nom du preset actuellement actif (binding ComboBox).
    /// </summary>
    [ObservableProperty]
    private string? _activePresetName;

    /// <summary>
    /// Texte de recherche du panneau (B5-D). Filtre les groupes/materiaux affiches
    /// via une projection — jamais la collection persistee elle-meme.
    /// </summary>
    [ObservableProperty]
    private string _searchText = string.Empty;

    /// <summary>
    /// Collection bindee par le TreeView (B5-D).
    /// Recherche vide : reference directe a PresetGroups (binding vivant sur la source).
    /// Recherche active : projection de clones legers de PresetGroupDto dont Materials
    /// referencent les MEMES instances de PresetMaterialDto — PresetGroups EST
    /// _collection.Groups, la collection persistee par l'AutoSave : il est INTERDIT
    /// de la filtrer en place ou d'y poser des flags UI.
    /// </summary>
    [ObservableProperty]
    private ObservableCollection<PresetGroupDto> _displayedGroups = new();

    /// <summary>
    /// True quand la recherche est active et qu'aucun groupe/materiau ne matche (UI-m14).
    /// </summary>
    [ObservableProperty]
    private bool _isSearchNoResult;

    /// <summary>
    /// Mapping clone de projection -> groupe source (B5-D), pour que la selection et
    /// le drag-and-drop pendant une recherche active operent sur les groupes sources.
    /// </summary>
    private readonly Dictionary<PresetGroupDto, PresetGroupDto> _projectedGroupSources = new();

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
    /// Supprime le preset actif et son fichier apres confirmation (action destructive).
    /// </summary>
    [RelayCommand]
    private void SupprimerPreset()
    {
        if (string.IsNullOrEmpty(ActivePresetName)) return;

        if (!DialogService.Confirm(
                $"Supprimer le preset \"{ActivePresetName}\" et son fichier ?",
                "Supprimer le preset"))
            return;

        // net48 : IsNullOrEmpty n'a pas [NotNullWhen(false)], ActivePresetName est prouve non null ici.
        var name = ActivePresetName!;
        _presetService.DeletePreset(name);
        PresetNames.Remove(name);

        if (PresetNames.Count > 0)
        {
            ActivePresetName = PresetNames[0];
        }
        else
        {
            ActivePresetName = null;
            PresetGroups.Clear();
            _collection = null;
        }

        StatusMessage = $"Preset \"{name}\" supprimé.";
    }

    /// <summary>
    /// Cree un nouveau preset via un dialog de saisie.
    /// </summary>
    [RelayCommand]
    private void CreerPreset()
    {
        var dialog = new CreateNameDialog();
        dialog.Title = "Nouveau preset";
        dialog.SetPrompt("Nom du preset :");
        dialog.Owner = App.MainWindow;

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.EnteredName))
        {
            var name = dialog.EnteredName;

            // Verifier si le nom existe deja
            if (PresetNames.Contains(name))
            {
                StatusMessage = $"Le preset \"{name}\" existe déjà.";
                return;
            }

            _collection = _presetService.CreatePreset(name);
            PresetNames.Add(name);
            ActivePresetName = name;
            PresetGroups = _collection.Groups;
            StatusMessage = $"Preset \"{name}\" créé.";
        }
    }

    /// <summary>
    /// Charge un fichier preset externe via OpenFileDialog.
    /// Copie le fichier dans le dossier presets pour la persistence.
    /// </summary>
    [RelayCommand]
    private void ChargerPresetExterne()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog
        {
            Title = "Charger un preset externe",
            Filter = "Fichiers JSON (*.json)|*.json",
            DefaultExt = ".json"
        };

        if (dialog.ShowDialog() != true) return;

        try
        {
            var sourcePath = dialog.FileName;
            var presetName = Path.GetFileNameWithoutExtension(sourcePath);
            var destDir = _presetService.GetPresetsDirectory();
            var destPath = Path.Combine(destDir, presetName + ".json");

            // DON-09 : valider le JSON AVANT toute copie dans le dossier projet
            var json = File.ReadAllText(sourcePath);
            if (!PresetService.IsValidPresetJson(json))
            {
                StatusMessage = $"Fichier invalide : \"{Path.GetFileName(sourcePath)}\" n'est pas un preset JSON lisible. Import abandonné.";
                return;
            }

            // DON-09 : collision avec un preset existant -> confirmation avant ecrasement
            if (File.Exists(destPath) && !DialogService.Confirm(
                    $"Un preset nommé \"{presetName}\" existe déjà.\nL'écraser avec le fichier importé ?",
                    "Preset existant"))
            {
                return;
            }

            // Copier le fichier dans le dossier presets
            File.Copy(sourcePath, destPath, overwrite: true);

            // Mettre a jour la liste et activer le preset
            if (!PresetNames.Contains(presetName))
                PresetNames.Add(presetName);

            ActivePresetName = presetName;
            StatusMessage = $"Preset \"{presetName}\" chargé depuis fichier externe.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Erreur chargement preset externe : {ex.Message}";
        }
    }

    /// <summary>
    /// Cree un nouveau groupe de presets via un dialog de saisie (D-11).
    /// </summary>
    [RelayCommand]
    private void CreerGroupe()
    {
        var dialog = new CreateNameDialog();
        dialog.Title = "Nouveau groupe";
        dialog.SetPrompt("Nom du groupe :");
        dialog.Owner = App.MainWindow;

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(dialog.EnteredName))
        {
            var group = new PresetGroupDto { GroupName = dialog.EnteredName };
            PresetGroups.Add(group);
            _collection?.Groups.Add(group);
            StatusMessage = $"Groupe \"{group.GroupName}\" créé.";
            AutoSave();
        }
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
            StatusMessage = "Créez d'abord un groupe de presets.";
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
                    dialog.SelectedMaterials.Count > 0 &&
                    dialog.SelectedGroup != null)
                {
                    foreach (var mat in dialog.SelectedMaterials)
                    {
                        dialog.SelectedGroup.Materials.Add(mat);
                    }
                    StatusMessage = dialog.SelectedMaterials.Count == 1
                        ? $"\"{dialog.SelectedMaterials[0].MaterialName}\" ajouté à \"{dialog.SelectedGroup.GroupName}\"."
                        : $"{dialog.SelectedMaterials.Count} matériaux ajoutés à \"{dialog.SelectedGroup.GroupName}\".";
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
    /// B5-D : pendant une recherche active, le TreeView affiche des clones de
    /// projection — resoudre systematiquement vers les groupes sources avant
    /// toute mutation (sinon le materiau serait ajoute a un clone jetable).
    /// </summary>
    public void MoveMaterial(PresetMaterialDto material, PresetGroupDto sourceGroup, PresetGroupDto targetGroup)
    {
        sourceGroup = ResolveSourceGroup(sourceGroup);
        targetGroup = ResolveSourceGroup(targetGroup);
        if (sourceGroup == targetGroup) return;

        sourceGroup.Materials.Remove(material);
        targetGroup.Materials.Add(material);
        StatusMessage = $"\"{material.MaterialName}\" déplacé vers \"{targetGroup.GroupName}\".";
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
                    StatusMessage = $"\"{newMat.MaterialName}\" dupliqué dans \"{group.GroupName}\".";
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
    /// UI-m11 : confirmation prealable, coherente avec les autres suppressions
    /// (couvre le menu contextuel ET le bouton "-" via SupprimerSelection).
    /// </summary>
    [RelayCommand]
    private void SupprimerMateriau(PresetMaterialDto? material)
    {
        if (material == null) return;

        if (!DialogService.Confirm(
                $"Supprimer \"{material.MaterialName}\" du preset ?",
                "Supprimer du preset"))
            return;

        var group = FindGroupContaining(material);
        if (group != null)
        {
            group.Materials.Remove(material);
            StatusMessage = $"\"{material.MaterialName}\" supprimé de \"{group.GroupName}\".";

            if (SelectedPresetMaterial == material)
                SelectedPresetMaterial = null;

            AutoSave();
        }
    }

    /// <summary>
    /// Supprime la selection courante : si c'est un materiau, le retire du groupe.
    /// Si c'est un groupe, demande a l'utilisateur quoi faire des materiaux.
    /// </summary>
    [RelayCommand]
    private void SupprimerSelection()
    {
        // Cas 1 : un materiau est selectionne
        if (SelectedPresetMaterial != null)
        {
            SupprimerMateriau(SelectedPresetMaterial);
            return;
        }

        // Cas 2 : un groupe est selectionne
        if (SelectedGroup != null && PresetGroups.Contains(SelectedGroup))
        {
            SupprimerGroupe(SelectedGroup);
        }
    }

    /// <summary>
    /// Supprime un groupe apres avoir resolu le sort de ses materiaux (MAINT-10).
    /// </summary>
    private void SupprimerGroupe(PresetGroupDto group)
    {
        if (group.Materials.Count > 0 && !ResoudreSortDesMateriaux(group))
            return;

        PresetGroups.Remove(group);
        SelectedGroup = null;
        SelectedPresetMaterial = null;
        StatusMessage = $"Groupe \"{group.GroupName}\" supprimé.";
        AutoSave();
    }

    /// <summary>
    /// Demande a l'utilisateur quoi faire des materiaux d'un groupe a supprimer (MAINT-10) :
    /// transfert vers un autre groupe (Oui), suppression avec le groupe (Non),
    /// ou abandon (Annuler). Retourne true si la suppression peut continuer.
    /// S'il n'existe aucun autre groupe, la suppression continue directement
    /// (comportement d'origine).
    /// </summary>
    private bool ResoudreSortDesMateriaux(PresetGroupDto group)
    {
        var otherGroups = PresetGroups.Where(g => g != group).ToList();
        if (otherGroups.Count == 0)
            return true;

        var result = DialogService.ConfirmWithCancel(
            $"Le groupe \"{group.GroupName}\" contient {group.Materials.Count} matériau(x).\n\n" +
            "Oui = Transférer les matériaux dans un autre groupe\n" +
            "Non = Supprimer le groupe et ses matériaux\n" +
            "Annuler = Ne rien faire",
            "Supprimer le groupe");

        if (result == null)
            return false;

        // "Non" : supprimer le groupe et ses materiaux
        if (result == false)
            return true;

        // "Oui" : transferer dans un autre groupe choisi par l'utilisateur
        var targetGroup = ChoisirGroupeCible(otherGroups);
        if (targetGroup == null)
            return false;

        foreach (var mat in group.Materials.ToList())
        {
            targetGroup.Materials.Add(mat);
        }
        StatusMessage = $"{group.Materials.Count} matériau(x) transféré(s) dans \"{targetGroup.GroupName}\".";
        return true;
    }

    /// <summary>
    /// Choisit le groupe cible d'un transfert de materiaux (MAINT-10) :
    /// choix direct s'il n'y a qu'un candidat, sinon dialog avec ComboBox.
    /// Retourne null si l'utilisateur annule.
    /// </summary>
    private static PresetGroupDto? ChoisirGroupeCible(List<PresetGroupDto> otherGroups)
    {
        if (otherGroups.Count == 1)
            return otherGroups[0];

        var dialog = new ChooseGroupDialog();
        dialog.Owner = App.MainWindow;
        dialog.SetGroups(otherGroups);
        dialog.PreselectGroup(otherGroups[0]);

        if (dialog.ShowDialog() != true || dialog.SelectedGroup == null)
            return null;

        return dialog.SelectedGroup;
    }

    /// <summary>
    /// Met a jour SelectedPresetMaterial et SelectedGroup quand la selection TreeView change.
    /// </summary>
    [RelayCommand]
    private void TreeViewSelectionChanged(object? param)
    {
        SelectedPresetMaterial = param as PresetMaterialDto;

        // Tracker aussi le groupe selectionne
        // B5-D : pendant une recherche, le TreeView fournit un clone de projection —
        // resoudre vers le groupe source pour que suppression/ajout operent sur la
        // collection persistee.
        if (param is PresetGroupDto group)
        {
            SelectedGroup = ResolveSourceGroup(group);
        }
        else if (param is PresetMaterialDto mat)
        {
            SelectedGroup = FindGroupContaining(mat);
        }
    }

    // --- Recherche (B5-D) ---

    /// <summary>
    /// Efface la recherche (bouton ✕ et lien de l'etat « aucun resultat », UI-m14).
    /// </summary>
    [RelayCommand]
    private void EffacerRecherche()
    {
        SearchText = string.Empty;
    }

    /// <summary>
    /// Reconstruit la collection affichee par le TreeView (B5-D).
    /// Recherche vide : re-binde la collection source telle quelle.
    /// Recherche active : projection de clones legers (GroupName copie) dont Materials
    /// referencent les memes instances filtrees. Un groupe reste visible si son nom
    /// matche (tous ses materiaux sont alors montres) OU si un de ses materiaux matche.
    /// </summary>
    private void UpdateDisplayedGroups()
    {
        _projectedGroupSources.Clear();

        if (string.IsNullOrWhiteSpace(SearchText))
        {
            DisplayedGroups = PresetGroups;
            IsSearchNoResult = false;
            return;
        }

        var projection = new ObservableCollection<PresetGroupDto>();
        foreach (var group in PresetGroups)
        {
            bool groupNameMatches = SearchMatcher.Matches(group.GroupName, SearchText);
            var materials = groupNameMatches
                ? group.Materials.ToList()
                : group.Materials.Where(m => SearchMatcher.Matches(m.MaterialName, SearchText)).ToList();

            if (!groupNameMatches && materials.Count == 0)
                continue;

            var clone = new PresetGroupDto
            {
                GroupName = group.GroupName,
                Materials = new ObservableCollection<PresetMaterialDto>(materials)
            };
            _projectedGroupSources[clone] = group;
            projection.Add(clone);
        }

        DisplayedGroups = projection;
        IsSearchNoResult = projection.Count == 0;
    }

    /// <summary>
    /// Resout un groupe potentiellement issu de la projection de recherche vers son
    /// groupe source persiste (B5-D). Hors recherche, retourne le groupe tel quel.
    /// Internal : reutilise par le code-behind drag-and-drop de RightPanelView.
    /// </summary>
    internal PresetGroupDto ResolveSourceGroup(PresetGroupDto group)
    {
        return _projectedGroupSources.TryGetValue(group, out var source) ? source : group;
    }

    partial void OnSearchTextChanged(string value)
    {
        UpdateDisplayedGroups();
    }

    partial void OnPresetGroupsChanged(ObservableCollection<PresetGroupDto> value)
    {
        // Changement de preset actif : la projection (ou le binding direct) suit
        UpdateDisplayedGroups();
    }

    /// <summary>
    /// Charge les presets depuis le systeme multi-preset.
    /// Migration: si un ancien chemin single-file existe, il est utilise comme fallback.
    /// </summary>
    private void LoadPresets()
    {
        // Charger la liste des presets disponibles
        var availablePresets = _presetService.ListPresets();
        PresetNames = new ObservableCollection<string>(availablePresets);

        // Charger les settings pour connaitre le preset actif
        var settings = _presetService.LoadSettings();

        if (availablePresets.Count > 0)
        {
            // Utiliser le preset actif ou le premier disponible
            var targetName = settings.ActivePresetName ?? string.Empty;
            if (string.IsNullOrEmpty(targetName) || !availablePresets.Contains(targetName))
                targetName = availablePresets[0];

            var loaded = _presetService.LoadPreset(targetName);
            if (loaded == null)
            {
                // Fichier illisible : quarantaine faite cote service, AutoSave bloque (DON-02)
                _presetLoadFailed = true;
                _collection = PresetService.GetDefaultCollection();
                StatusMessage = $"Preset \"{targetName}\" illisible : fichier mis de côté (.corrupt), sauvegarde automatique désactivée.";
            }
            else
            {
                _presetLoadFailed = false;
                _collection = loaded;
            }
            ActivePresetName = targetName;
        }
        else
        {
            // Fallback : essayer l'ancien systeme single-file
            var storedPath = settings.PresetFilePath;
            if (storedPath != null && File.Exists(storedPath))
            {
                _collection = PresetService.Load(storedPath);

                // Migrer vers le nouveau systeme
                var migratedName = "Preset migre";
                _presetService.SavePreset(migratedName, _collection);
                PresetNames.Add(migratedName);
                ActivePresetName = migratedName;
            }
            else
            {
                // Creer un preset par defaut
                var defaultName = "Preset par defaut";
                _collection = _presetService.CreatePreset(defaultName);
                PresetNames.Add(defaultName);
                ActivePresetName = defaultName;
            }
        }

        PresetGroups = _collection.Groups;
    }

    /// <summary>
    /// Reagit au changement de preset actif dans le ComboBox.
    /// </summary>
    partial void OnActivePresetNameChanged(string? value)
    {
        if (string.IsNullOrEmpty(value)) return;

        var loaded = _presetService.LoadPreset(value!);
        if (loaded == null)
        {
            // Fichier illisible : quarantaine faite cote service, AutoSave bloque (DON-02)
            _presetLoadFailed = true;
            _collection = PresetService.GetDefaultCollection();
            StatusMessage = $"Preset \"{value}\" illisible : fichier mis de côté (.corrupt), sauvegarde automatique désactivée.";
        }
        else
        {
            _presetLoadFailed = false;
            _collection = loaded;
        }
        PresetGroups = _collection.Groups;

        // Persister le choix dans les settings
        // FIA-03 : setter binde sur le thread UI du process Revit — proteger l'ecriture.
        try
        {
            var settings = _presetService.LoadSettings();
            settings.ActivePresetName = value;
            _presetService.SaveSettings(settings);
        }
        catch (Exception ex)
        {
            LogService.Error("Echec de sauvegarde des parametres (preset actif)", ex);
            StatusMessage = $"Échec de sauvegarde des paramètres : {ex.Message}";
        }

        // Mettre a jour le titre
        PanelTitle = $"Matériaux Preset - {value}";
    }

    /// <summary>
    /// Sauvegarde automatique apres chaque modification.
    /// Utilise le systeme multi-preset (sauvegarde dans le fichier du preset actif).
    /// </summary>
    private void AutoSave()
    {
        // B5-D : toute modification pendant une recherche active rafraichit la
        // projection affichee (AutoSave est appele apres chaque mutation, meme
        // quand la sauvegarde elle-meme est bloquee par DON-02).
        if (!string.IsNullOrWhiteSpace(SearchText))
            UpdateDisplayedGroups();

        // Chargement en echec : ne surtout pas ecraser le fichier (DON-02)
        if (_presetLoadFailed) return;
        if (_collection == null || string.IsNullOrEmpty(ActivePresetName)) return;

        // FIA-03 : ecriture declenchee depuis le thread UI du process Revit —
        // toute exception I/O doit etre signalee, jamais propagee a l'hote.
        try
        {
            _presetService.SavePreset(ActivePresetName!, _collection);
        }
        catch (Exception ex)
        {
            LogService.Error($"Echec de sauvegarde du preset \"{ActivePresetName}\"", ex);
            StatusMessage = $"Échec de sauvegarde du preset \"{ActivePresetName}\" : {ex.Message}";
        }
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
    /// Internal (MAINT-09) : reutilise par le code-behind drag-and-drop de RightPanelView.
    /// </summary>
    internal PresetGroupDto? FindGroupContaining(PresetMaterialDto material)
    {
        foreach (var group in PresetGroups)
        {
            if (group.Materials.Contains(material))
                return group;
        }
        return null;
    }
}
