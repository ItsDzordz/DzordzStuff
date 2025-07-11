using System.Linq;
using System.Numerics;
using System.Text;
using Content.Client.Guidebook;
using Content.Client.Paint;
using Content.Client.Players.PlayTimeTracking;
using Content.Client.Stylesheets;
using Content.Client.UserInterface.Systems.Guidebook;
using Content.Shared.Clothing.Loadouts.Prototypes;
using Content.Shared.Clothing.Loadouts.Systems;
using Content.Shared.Customization.Systems;
using Content.Shared.Paint;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Client.AutoGenerated;
using Robust.Client.Graphics;
using Robust.Client.UserInterface;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Client.UserInterface.XAML;
using Robust.Shared.Configuration;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Client.Lobby.UI;


[GenerateTypedNameReferences]
public sealed partial class LoadoutPreferenceSelector : Control
{
    public const string DefaultLoadoutInfoGuidebook = "LoadoutInfo";

    public EntityUid DummyEntityUid;
    public readonly IEntityManager _entityManager; // Floof - making this public because fuck privacy and fuck introducing even more merge conflicts in the future

    public LoadoutPrototype Loadout { get; }

    private LoadoutPreference _preference = null!;
    public LoadoutPreference Preference
    {
        get => _preference;
        set
        {
            _preference = value;
            // Floof - moved to LoadoutPreferenceSelectorSpecial
            // NameEdit.Text = value.CustomName ?? "";
            // DescriptionEdit.TextRope = new Rope.Leaf(value.CustomDescription ?? "");
            // ColorEdit.Color = Color.FromHex(value.CustomColorTint, Color.White);
            _special?.UpdateState(); // Floof
            if (value.CustomColorTint != null)
                UpdatePaint(); // Floof - simpler method call
            HeirloomButton.Pressed = value.CustomHeirloom ?? false;
            PreferenceButton.Pressed = value.Selected;

            // Floof - close the special menu if the loadout is de-selected, since that means editing the loadout will do nothing
            HeadingButton.Disabled = !value.Selected;
            if (!value.Selected)
            {
                HeadingButton.Pressed = false;
                _special?.Dispose(); // TODO code duplication, RT won't fire the toggle event when changing Pressed; don't wanna deal with it
                _special = null;
            }
        }
    }

    public bool Valid;
    private bool _showUnusable;
    public bool ShowUnusable
    {
        get => _showUnusable;
        set
        {
            _showUnusable = value;
            Visible = Valid && _wearable || _showUnusable;
            // PreferenceButton.RemoveStyleClass(StyleBase.ButtonDanger);
            // PreferenceButton.AddStyleClass(Valid ? "" : StyleBase.ButtonDanger);
            // Floofstation - the above, but less performance-intensive
            if (!Valid && !PreferenceButton.HasStyleClass(StyleBase.ButtonDanger))
                PreferenceButton.AddStyleClass(StyleBase.ButtonDanger);
            else if (Valid && PreferenceButton.HasStyleClass(StyleBase.ButtonDanger))
                PreferenceButton.RemoveStyleClass(StyleBase.ButtonDanger);
        }
    }

    private bool _wearable;
    public bool Wearable
    {
        get => _wearable;
        set
        {
            _wearable = value;
            Visible = Valid && _wearable || _showUnusable;
            // PreferenceButton.RemoveStyleClass(StyleBase.ButtonCaution);
            // PreferenceButton.AddStyleClass(_wearable ? "" : StyleBase.ButtonCaution);
            // Floofstation - the above, but less performance-intensive
            if (!Valid && !PreferenceButton.HasStyleClass(StyleBase.ButtonCaution))
                PreferenceButton.RemoveStyleClass(StyleBase.ButtonCaution);
            else if (Valid && PreferenceButton.HasStyleClass(StyleBase.ButtonCaution))
                PreferenceButton.AddStyleClass(StyleBase.ButtonCaution);
        }
    }

    public event Action<LoadoutPreference>? PreferenceChanged;
    // Floofstation section
    // Adding this here only because C# won't allow it from other classes otherwise
    public void InvokePreferenceChanged() => PreferenceChanged?.Invoke(Preference);
    // This holds the special edit fields and is created at runtime
    private LoadoutPreferenceSelectorSpecial? _special = null;

    // Constructor parameters that were passed but not stored
    private readonly JobPrototype _highJob;
    private readonly HumanoidCharacterProfile _profile;
    private readonly Dictionary<string, EntityUid> _entities;
    private readonly IPrototypeManager _prototypeManager;
    private readonly IConfigurationManager _configManager;
    private readonly CharacterRequirementsSystem _characterRequirementsSystem;
    private readonly JobRequirementsManager _jobRequirementsManager;
    // Floofstation section end

    public LoadoutPreferenceSelector(LoadoutPrototype loadout, JobPrototype highJob,
        HumanoidCharacterProfile profile, ref Dictionary<string, EntityUid> entities,
        IEntityManager entityManager, IPrototypeManager prototypeManager, IConfigurationManager configManager,
        CharacterRequirementsSystem characterRequirementsSystem, JobRequirementsManager jobRequirementsManager)
    {
        RobustXamlLoader.Load(this);

        _entityManager = entityManager;
        Loadout = loadout;

        // Floofstation section
        _highJob = highJob;
        _profile = profile;
        _entities = entities; // This is fine(tm), so long as references in C# are not pointers
        _prototypeManager = prototypeManager;
        _configManager = configManager;
        _characterRequirementsSystem = characterRequirementsSystem;
        _jobRequirementsManager = jobRequirementsManager;
        // Floofstation section end
    }

    // Floofstation - EVERYTHING INSIDE THIS METHOD USED TO BE IN THE CONSTRUCTOR ABOVE.
    private void ActuallyCreateEverythingInADeferredMannerThatWillNotLagTheLobby()
    {
        // Show/hide the special menu and items depending on what's allowed
        HeirloomButton.Visible = Loadout.CanBeHeirloom;
        SpecialMenu.Visible = Loadout.CustomName || Loadout.CustomDescription || Loadout.CustomColorTint;
        // Floof - moved to LoadoutPreferenceSelectorSpecial
        // SpecialName.Visible = Loadout.CustomName;
        // SpecialDescription.Visible = Loadout.CustomDescription;
        // SpecialColorTintToggle.Visible = Loadout.CustomColorTint;


        SpriteView previewLoadout;
        if (!_entities.TryGetValue(Loadout.ID + 0, out var dummyLoadoutItem))
        {
            // Get the first item in the loadout to be the preview
            dummyLoadoutItem = _entityManager.SpawnEntity(Loadout.Items.First(), MapCoordinates.Nullspace);
            _entities.Add(Loadout.ID + 0, dummyLoadoutItem);

            // Create a sprite preview of the loadout item
            previewLoadout = new SpriteView
            {
                Scale = new Vector2(1, 1),
                OverrideDirection = Direction.South,
                VerticalAlignment = VAlignment.Center,
                SizeFlagsStretchRatio = 1,
            };
            previewLoadout.SetEntity(dummyLoadoutItem);
        }
        else
        {
            // Create a sprite preview of the loadout item
            previewLoadout = new SpriteView
            {
                Scale = new Vector2(1, 1),
                OverrideDirection = Direction.South,
                VerticalAlignment = VAlignment.Center,
                SizeFlagsStretchRatio = 1,
            };
            previewLoadout.SetEntity(dummyLoadoutItem);
        }
        DummyEntityUid = dummyLoadoutItem;

        _entityManager.EnsureComponent<AppearanceComponent>(dummyLoadoutItem);
        _entityManager.EnsureComponent<PaintedComponent>(dummyLoadoutItem, out var paint);

        var loadoutName =
            Loc.GetString($"loadout-name-{Loadout.ID}") == $"loadout-name-{Loadout.ID}"
                ? _entityManager.GetComponent<MetaDataComponent>(dummyLoadoutItem).EntityName
                : Loc.GetString($"loadout-name-{Loadout.ID}");
        var loadoutDesc =
            !Loc.TryGetString($"loadout-description-{Loadout.ID}", out var description)
                ? _entityManager.GetComponent<MetaDataComponent>(dummyLoadoutItem).EntityDescription
                : description;


        // Manage the info button
        void UpdateGuidebook() => GuidebookButton.Visible =
            _prototypeManager.HasIndex<GuideEntryPrototype>(Loadout.GuideEntry);
        UpdateGuidebook();
        _prototypeManager.PrototypesReloaded += _ => UpdateGuidebook();

        GuidebookButton.OnPressed += _ =>
        {
            if (!_prototypeManager.TryIndex<GuideEntryPrototype>(Loadout.GuideEntry, out var guideRoot))
                return;

            var guidebookController = UserInterfaceManager.GetUIController<GuidebookUIController>();
            //TODO: Don't close the guidebook if its already open, just go to the correct page
            guidebookController.ToggleGuidebook(
                new Dictionary<string, GuideEntry> { { Loadout.GuideEntry, guideRoot } },
                includeChildren: true,
                selected: Loadout.GuideEntry);
        };

        // Create a checkbox to get the loadout
        PreferenceButton.AddChild(new BoxContainer
        {
            Children =
            {
                new Label
                {
                    Text = Loadout.Cost.ToString(),
                    StyleClasses = { StyleBase.StyleClassLabelHeading },
                    MinWidth = 32,
                    MaxWidth = 32,
                    ClipText = true,
                    Margin = new Thickness(0, 0, 8, 0),
                },
                new PanelContainer
                {
                    PanelOverride = new StyleBoxFlat { BackgroundColor = Color.FromHex("#2f2f2f") },
                    Children =
                    {
                        previewLoadout,
                    },
                },
                new Label
                {
                    Text = loadoutName,
                    Margin = new Thickness(8, 0, 0, 0),
                },
            },
        });
        PreferenceButton.OnToggled += args =>
        {
            if (args.Pressed == _preference.Selected) // Floofstation
                return;

            _preference.Selected = args.Pressed;
            PreferenceChanged?.Invoke(Preference);
        };
        HeirloomButton.OnToggled += args =>
        {
            if (args.Pressed == _preference.CustomHeirloom) // Floofstation
                return;

            _special?.Save();
            _preference.CustomHeirloom = args.Pressed ? true : null;
            PreferenceChanged?.Invoke(Preference);
        };

        // Floofstation section - making the heading button not just collapse/open the special menu, but also handle its creation and disposal
        HeadingButton.OnToggled += args =>
        {
            if (!args.Pressed)
            {
                // Destroy, annihilate
                _special?.Save(); // Because it's counterintuitive that the save button always has to be pressed to save the changes
                _special?.Dispose();
                _special = null;
            }
            else
            {
                _special = new LoadoutPreferenceSelectorSpecial(this);
                SpecialMenu.Body?.AddChild(_special);
            }
        };
        // Floofstation section end

        // Floofstation - moved all the stuff below to LoadoutPreferenceSelectorSpecial
        // Update prefs cache when something changes
        // NameEdit.OnTextChanged += _ =>
        //     _preference.CustomName = string.IsNullOrEmpty(NameEdit.Text) ? null : NameEdit.Text;
        // DescriptionEdit.OnTextChanged += _ =>
        //     _preference.CustomDescription = string.IsNullOrEmpty(Rope.Collapse(DescriptionEdit.TextRope)) ? null : Rope.Collapse(DescriptionEdit.TextRope);
        // SpecialColorTintToggle.OnToggled += args =>
        //     ColorEdit.Visible = args.Pressed;
        // ColorEdit.OnColorChanged += _ =>
        // {
        //     _preference.CustomColorTint = SpecialColorTintToggle.Pressed ? ColorEdit.Color.ToHex() : null;
        //     UpdatePaint(new Entity<PaintedComponent>(dummyLoadoutItem, paint), entityManager);
        // // };
        //
        // NameEdit.PlaceHolder = loadoutName;
        // DescriptionEdit.Placeholder = new Rope.Leaf(Loc.GetString(loadoutDesc));


        var tooltip = new StringBuilder();
        // Add the loadout description to the tooltip if there is one
        if (!string.IsNullOrEmpty(loadoutDesc))
            tooltip.Append($"{Loc.GetString(loadoutDesc)}");

        // Get requirement reasons
        _characterRequirementsSystem.CheckRequirementsValid(
            Loadout.Requirements, _highJob, _profile, new Dictionary<string, TimeSpan>(),
            _jobRequirementsManager.IsWhitelisted(), Loadout,
            _entityManager, _prototypeManager, _configManager,
            out var reasons);

        // Add requirement reasons to the tooltip
        foreach (var reason in reasons)
            tooltip.Append($"\n{reason}");

        // Combine the tooltip and format it in the checkbox supplier
        if (tooltip.Length > 0)
        {
            var formattedTooltip = new Tooltip();
            formattedTooltip.SetMessage(FormattedMessage.FromMarkupPermissive(tooltip.ToString()));
            PreferenceButton.TooltipSupplier = _ => formattedTooltip;
        }
    }

    // Floofstation section - delegation of the above
    private bool _layoutInitialized;
    protected override Vector2 MeasureCore(Vector2 availableSize)
    {
        // We override this here to inject additional elements BEFORE this element measures itself
        // This way we can ensure that the first measure is valid,
        // AND ALSO ensure that redundant stuff is not initilalized unless this element is visible and is about to get drawn
        if (!_layoutInitialized && Visible)
        {
            ActuallyCreateEverythingInADeferredMannerThatWillNotLagTheLobby();
            _layoutInitialized = true;
        }

        return base.MeasureCore(availableSize);
    }
    // Floofstation section end

    private bool _initialized;
    protected override void FrameUpdate(FrameEventArgs args)
    {
        if (_initialized || SpecialMenu.Heading == null)
            return;

        ActuallyCreateEverythingInADeferredMannerThatWillNotLagTheLobby(); // Floofstation

        // Move the special editor
        var heading = SpecialMenu.Heading;
        heading.Orphan();
        ButtonGroup.AddChild(heading);
        GuidebookButton.Orphan();
        ButtonGroup.AddChild(GuidebookButton);

        // Floof - no clue why this even existed. Replaced with Expand parameters.
        // These guys are here too for reasons
        // HeadingButton.SetHeight = HeirloomButton.SetHeight = GuidebookButton.SetHeight = PreferenceButton.Size.Y;
        // SpecialColorTintToggle.Pressed = ColorEdit.Visible = _preference.CustomColorTint != null;

        _initialized = true;
    }

    private void UpdatePaint(Entity<PaintedComponent> entity, IEntityManager entityManager)
    {
        if (_preference.CustomColorTint != null)
        {
            entity.Comp.Color = Color.FromHex(_preference.CustomColorTint);
            entity.Comp.Enabled = true;
        }
        else
            entity.Comp.Enabled = false;

        var app = entityManager.System<SharedAppearanceSystem>();
        app.TryGetData(entity, PaintVisuals.Painted, out bool value);
        app.SetData(entity, PaintVisuals.Painted, !value);
    }

    // Floofstation convenience method
    public void UpdatePaint()
    {
        // Can occur if preference is set before the loadout is initialized. Shouldn't matter, hopefully.
        if (DummyEntityUid is not { Valid: true })
            return;

        var paint = _entityManager.EnsureComponent<PaintedComponent>(DummyEntityUid);
        UpdatePaint((DummyEntityUid, paint), _entityManager);
    }
}
