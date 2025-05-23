using Robust.Server.GameObjects;
using Robust.Server.Maps;
using Robust.Shared.Configuration;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Content.Server.GameTicking;
using Content.Server.StationEvents.Components;
using Content.Shared.Salvage;
using System.Linq;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.GameTicking.Components;
using Content.Shared.CCVar;
using Content.Shared.Parallax.Biomes;
using Robust.Shared.Map;


namespace Content.Server.StationEvents.Events;

public sealed class PirateRadioSpawnRule : StationEventSystem<PirateRadioSpawnRuleComponent>
{
    [Dependency] private readonly MapLoaderSystem _map = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly IPrototypeManager _prototypeManager = default!;
    [Dependency] private readonly IConfigurationManager _confMan = default!;
    [Dependency] private readonly GameTicker _gameTicker = default!;
    [Dependency] private readonly TransformSystem _xform = default!;
    [Dependency] private readonly StationSystem _stations = default!;
    [Dependency] private readonly MapSystem _mapSystem = default!;

    protected override void Started(EntityUid uid, PirateRadioSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        var stations = _gameTicker.GetSpawnableStations();

        // Remove any station from the list that is on a Planet's surface.
        foreach (var station in stations.ToList())
            if (HasComp<BiomeComponent>(Transform(station).MapUid))
                stations.Remove(station);

        if (stations.Count <= 0)
            return;

        // Floof - the station's TransformComponent does not actually store the MapID. In fact, it doesn't store anything and remains uninitialized.
        // To circumvent this issue, we instead use the transform component of the largest grid on the map.
        var chosenStation = _random.Pick(stations);
        if (!TryComp<StationDataComponent>(chosenStation, out var stationData)
            || _stations.GetLargestGrid(stationData) is not { } targetStation)
            return;

        var targetMapId = Transform(targetStation).MapID;
        if (!_mapSystem.MapExists(targetMapId))
            return;

        var randomOffset = _random.NextVector2(component.MinimumDistance, component.MaximumDistance);
        var outpostOptions = new MapLoadOptions
        {
            Offset = _xform.GetWorldPosition(targetStation) + randomOffset,
            LoadMap = false,
        };

        if (!_map.TryLoad(Transform(targetStation).MapID, _random.Pick(component.PirateRadioShuttlePath), out var outpostids, outpostOptions))
            return;

        SpawnDebris(component, outpostids);
    }

    private void SpawnDebris(PirateRadioSpawnRuleComponent component, IReadOnlyList<EntityUid> outpostids)
    {
        if (_confMan.GetCVar(CCVars.WorldgenEnabled)
            || component.DebrisCount <= 0)
            return;

        foreach (var id in outpostids)
        {
            var outpostaabb = _xform.GetWorldPosition(id);
            var k = 0;

            while (k < component.DebrisCount)
            {
                var debrisRandomOffset = _random.NextVector2(component.MinimumDebrisDistance, component.MaximumDebrisDistance);
                var randomer = _random.NextVector2(component.DebrisMinimumOffset, component.DebrisMaximumOffset); //Second random vector to ensure the outpost isn't perfectly centered in the debris field
                var debrisOptions = new MapLoadOptions
                {
                    Offset = outpostaabb + debrisRandomOffset + randomer,
                    LoadMap = false,
                };

                var salvPrototypes = _prototypeManager.EnumeratePrototypes<SalvageMapPrototype>().ToList();
                var salvageProto = _random.Pick(salvPrototypes);

                if (!_mapSystem.MapExists(GameTicker.DefaultMap))
                    return;

                // Round didn't start before running this, leading to a null-space test fail.
                if (GameTicker.DefaultMap == MapId.Nullspace)
                    return;

                if (!_map.TryLoad(GameTicker.DefaultMap, salvageProto.MapPath.ToString(), out _, debrisOptions))
                    return;

                k++;
            }
        }
    }

    protected override void Ended(EntityUid uid, PirateRadioSpawnRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        if (component.AdditionalRule != null)
            GameTicker.EndGameRule(component.AdditionalRule.Value);
    }
}
