using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Media.Media3D;

namespace Olympe.MaterialManager.Controls;

/// <summary>
/// Controle WPF qui affiche une sphere 3D eclairee reproduisant l'apercu Revit.
/// Parametres pris en compte :
/// - Couleur diffuse (MaterialColor)
/// - Texture bitmap (TextureSource) plaquee sur la sphere via UV mapping
/// - Puissance speculaire (SpecularPower) derivee du Shininess Revit
/// - Opacite (MaterialOpacity) derivee de la Transparence Revit
/// </summary>
public class MaterialSpherePreview : Viewport3D
{
    // ---- Dependency Properties ----

    public static readonly DependencyProperty MaterialColorProperty =
        DependencyProperty.Register(nameof(MaterialColor), typeof(Color), typeof(MaterialSpherePreview),
            new PropertyMetadata(Colors.Gray, OnAppearanceChanged));

    public static readonly DependencyProperty SpecularPowerProperty =
        DependencyProperty.Register(nameof(SpecularPower), typeof(double), typeof(MaterialSpherePreview),
            new PropertyMetadata(40.0, OnAppearanceChanged));

    public static readonly DependencyProperty MaterialOpacityProperty =
        DependencyProperty.Register(nameof(MaterialOpacity), typeof(double), typeof(MaterialSpherePreview),
            new PropertyMetadata(1.0, OnAppearanceChanged));

    public static readonly DependencyProperty TextureSourceProperty =
        DependencyProperty.Register(nameof(TextureSource), typeof(ImageSource), typeof(MaterialSpherePreview),
            new PropertyMetadata(null, OnAppearanceChanged));

    public Color MaterialColor
    {
        get => (Color)GetValue(MaterialColorProperty);
        set => SetValue(MaterialColorProperty, value);
    }

    public double SpecularPower
    {
        get => (double)GetValue(SpecularPowerProperty);
        set => SetValue(SpecularPowerProperty, value);
    }

    public double MaterialOpacity
    {
        get => (double)GetValue(MaterialOpacityProperty);
        set => SetValue(MaterialOpacityProperty, value);
    }

    public ImageSource? TextureSource
    {
        get => (ImageSource?)GetValue(TextureSourceProperty);
        set => SetValue(TextureSourceProperty, value);
    }

    // ---- Elements 3D internes ----

    private readonly MaterialGroup _materialGroup;
    private readonly GeometryModel3D _sphereModel;

    public MaterialSpherePreview()
    {
        // Camera perspective comme Revit : vue frontale legerement en hauteur
        Camera = new PerspectiveCamera
        {
            Position = new Point3D(0, 0.2, 3.0),
            LookDirection = new Vector3D(0, -0.06, -1),
            UpDirection = new Vector3D(0, 1, 0),
            FieldOfView = 40
        };

        var modelGroup = new Model3DGroup();

        // ---- Eclairage studio Revit ----
        // Cle principale : haut-gauche, blanc chaud
        modelGroup.Children.Add(new DirectionalLight
        {
            Color = Color.FromRgb(240, 238, 230),
            Direction = new Vector3D(-0.6, -0.7, -0.8)
        });

        // Remplissage : droite, bleu-gris froid (contraste chaud/froid comme Revit)
        modelGroup.Children.Add(new DirectionalLight
        {
            Color = Color.FromRgb(70, 75, 90),
            Direction = new Vector3D(0.7, 0.1, -0.5)
        });

        // Contour : arriere-haut, pour definir le bord de la sphere
        modelGroup.Children.Add(new DirectionalLight
        {
            Color = Color.FromRgb(45, 45, 55),
            Direction = new Vector3D(0.1, 0.4, 0.8)
        });

        // Lumiere du bas (reflet sol gris comme dans Revit)
        modelGroup.Children.Add(new DirectionalLight
        {
            Color = Color.FromRgb(25, 25, 30),
            Direction = new Vector3D(0, 1, 0)
        });

        // Ambiante : evite le noir absolu
        modelGroup.Children.Add(new AmbientLight
        {
            Color = Color.FromRgb(35, 35, 40)
        });

        // ---- Materiau sphere ----
        _materialGroup = new MaterialGroup();
        RebuildMaterial();

        // ---- Sphere haute resolution ----
        _sphereModel = new GeometryModel3D
        {
            Geometry = CreateSphereMesh(1.0, 64, 64),
            Material = _materialGroup,
            BackMaterial = _materialGroup
        };
        modelGroup.Children.Add(_sphereModel);

        Children.Add(new ModelVisual3D { Content = modelGroup });
        ClipToBounds = true;
    }

    // ---- Mise a jour du materiau ----

    private static void OnAppearanceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is MaterialSpherePreview preview)
        {
            preview.RebuildMaterial();
        }
    }

    /// <summary>
    /// Reconstruit le MaterialGroup 3D a partir des proprietes actuelles.
    /// Gere : couleur/texture diffuse, speculaire, transparence.
    /// </summary>
    private void RebuildMaterial()
    {
        _materialGroup.Children.Clear();

        // 1. Materiau diffus : texture si disponible, sinon couleur unie
        Brush diffuseBrush;
        if (TextureSource != null)
        {
            diffuseBrush = new ImageBrush(TextureSource)
            {
                TileMode = TileMode.Tile,
                Stretch = Stretch.Fill
            };
        }
        else
        {
            diffuseBrush = new SolidColorBrush(MaterialColor);
        }

        // Appliquer l'opacite (transparence)
        diffuseBrush.Opacity = Clamp(MaterialOpacity, 0.05, 1.0);

        _materialGroup.Children.Add(new DiffuseMaterial(diffuseBrush));

        // 2. Materiau speculaire : reflet blanc, puissance basee sur Shininess
        double specPower = Clamp(SpecularPower, 1, 200);
        // Plus le materiau est brillant, plus le reflet est intense
        byte specIntensity = (byte)Math.Min(255, 120 + specPower);

        _materialGroup.Children.Add(new SpecularMaterial(
            new SolidColorBrush(Color.FromRgb(specIntensity, specIntensity, specIntensity)),
            specPower));

        // 3. Si transparent : ajouter un EmissiveMaterial subtil pour eviter la sphere noire
        if (MaterialOpacity < 0.5)
        {
            var emissiveColor = Color.FromArgb(40, MaterialColor.R, MaterialColor.G, MaterialColor.B);
            _materialGroup.Children.Add(new EmissiveMaterial(new SolidColorBrush(emissiveColor)));
        }

        // Mettre a jour le modele si deja cree
        if (_sphereModel != null)
        {
            _sphereModel.Material = _materialGroup;
            _sphereModel.BackMaterial = _materialGroup;
        }
    }

    // ---- Generation du maillage sphere ----

    private static double Clamp(double value, double min, double max) =>
        value < min ? min : value > max ? max : value;

    private static MeshGeometry3D CreateSphereMesh(double radius, int stacks, int slices)
    {
        var mesh = new MeshGeometry3D();

        for (int stack = 0; stack <= stacks; stack++)
        {
            double phi = Math.PI * stack / stacks;
            double y = radius * Math.Cos(phi);
            double ringRadius = radius * Math.Sin(phi);

            for (int slice = 0; slice <= slices; slice++)
            {
                double theta = 2 * Math.PI * slice / slices;
                double x = ringRadius * Math.Cos(theta);
                double z = ringRadius * Math.Sin(theta);

                mesh.Positions.Add(new Point3D(x, y, z));

                var normal = new Vector3D(x, y, z);
                normal.Normalize();
                mesh.Normals.Add(normal);

                // UV mapping spherique
                mesh.TextureCoordinates.Add(new Point(
                    (double)slice / slices,
                    (double)stack / stacks));
            }
        }

        int vertsPerRow = slices + 1;
        for (int stack = 0; stack < stacks; stack++)
        {
            for (int slice = 0; slice < slices; slice++)
            {
                int tl = stack * vertsPerRow + slice;
                int tr = tl + 1;
                int bl = tl + vertsPerRow;
                int br = bl + 1;

                mesh.TriangleIndices.Add(tl);
                mesh.TriangleIndices.Add(bl);
                mesh.TriangleIndices.Add(tr);

                mesh.TriangleIndices.Add(tr);
                mesh.TriangleIndices.Add(bl);
                mesh.TriangleIndices.Add(br);
            }
        }

        mesh.Freeze();
        return mesh;
    }
}
