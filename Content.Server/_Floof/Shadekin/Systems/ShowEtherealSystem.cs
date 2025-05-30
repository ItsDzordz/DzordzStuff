using Content.Shared._Floof.Shadekin;
using Content.Shared.Eye;
using Robust.Server.GameObjects;
using Content.Shared.Inventory.Events;
using Content.Shared.Interaction.Events;
using Content.Shared.Clothing.Components;

namespace Content.Server._Floof.Shadekin;

public sealed class ShowEtherealSystem : EntitySystem
{
    [Dependency] private readonly EyeSystem _eye = default!;
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ShowEtherealComponent, MapInitEvent>(OnInit);
        SubscribeLocalEvent<ShowEtherealComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<ShowEtherealComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<ShowEtherealComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<ShowEtherealComponent, InteractionAttemptEvent>(OnInteractionAttempt);
        SubscribeLocalEvent<ShowEtherealComponent, AttackAttemptEvent>(OnAttackAttempt);
    }

    private void OnInit(EntityUid uid, ShowEtherealComponent component, MapInitEvent args)
    {
        Toggle(uid, true);
    }

    public void OnShutdown(EntityUid uid, ShowEtherealComponent component, ComponentShutdown args)
    {
        Toggle(uid, false);
    }

    private void OnEquipped(EntityUid uid, ShowEtherealComponent component, GotEquippedEvent args)
    {
        if (!TryComp<ClothingComponent>(uid, out var clothing)
            || !clothing.Slots.HasFlag(args.SlotFlags))
            return;

        EnsureComp<ShowEtherealComponent>(args.Equipee);
    }

    private void OnUnequipped(EntityUid uid, ShowEtherealComponent component, GotUnequippedEvent args)
    {
        RemComp<ShowEtherealComponent>(args.Equipee);
    }

    private void Toggle(EntityUid uid, bool toggle)
    {
        if (!TryComp<EyeComponent>(uid, out var eye))
            return;

        if (toggle)
        {
            _eye.SetVisibilityMask(uid, eye.VisibilityMask | (int) (VisibilityFlags.Ethereal), eye);
            return;
        }
        else if (HasComp<EtherealComponent>(uid))
            return;

        _eye.SetVisibilityMask(uid, (int) VisibilityFlags.Normal, eye);
    }

    private void OnInteractionAttempt(EntityUid uid, ShowEtherealComponent component, InteractionAttemptEvent args)
    {
        if (HasComp<EtherealComponent>(args.Target))
            return;

        args.Cancelled = true;
    }

    private void OnAttackAttempt(EntityUid uid, ShowEtherealComponent component, AttackAttemptEvent args)
    {
        if (HasComp<EtherealComponent>(uid)
            || !HasComp<EtherealComponent>(args.Target))
            return;

        args.Cancel();
    }
}
