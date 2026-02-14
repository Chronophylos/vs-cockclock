using System;
using Vintagestory.API.Common;
using Vintagestory.API.Common.Entities;
using Vintagestory.API.Config;
using Vintagestory.API.Datastructures;
using Vintagestory.API.MathTools;
using Vintagestory.API.Server;
using Vintagestory.API.Util;
using Vintagestory.GameContent;

namespace CockClock;

public class EntityBehaviorAlarmClock(Entity entity) : EntityBehavior(entity)
{
    private readonly IWorldAccessor _world = entity.World;
    private bool _isNight;

    private bool IsAlarming
    {
        set => entity.WatchedAttributes.SetBool("is-alarming", value);
        get => entity.WatchedAttributes.GetBool("is-alarming");
    }

    public override string PropertyName() => "alarmclock";

    public override void Initialize(EntityProperties properties, JsonObject attributes)
    {
        base.Initialize(properties, attributes);
    }

    public override void AfterInitialized(bool onFirstSpawn)
    {
        base.AfterInitialized(onFirstSpawn);

        var dayLightStrength = _world.Calendar.GetDayLightStrength(entity.Pos.AsBlockPos);
        _isNight = dayLightStrength < 1;
    }

    public override void OnGameTick(float deltaTime)
    {
        base.OnGameTick(deltaTime);

        if (_world.Side != EnumAppSide.Server)
        {
            _world.Logger.Warning($"Server-only behaviour {nameof(EntityBehaviorAlarmClock)} called");
            return;
        }

        // must not be on fire
        if (entity.IsOnFire) return;

        // must be 'alive'
        if (!entity.Alive) return;

        // must be a rooster
        if (entity.Properties.Variant["age"] != "rooster") return;

        // When server says >= 1 the sun is out.
        var dayLightStrength = _world.Calendar.GetDayLightStrength(entity.Pos.AsBlockPos);
        if (dayLightStrength < 1)
        {
            _isNight = true;
            return;
        }
        // otherwise its day-ish

        // time change from night to day meaning its dusk now
        var isDusk = _isNight;
        _isNight = false;

        if (!isDusk) return;

        // we need the entire dance above to make sure the rooster calls every morning,
        // not just when players are sleeping

        entity.AnimManager.StopAnimation("Sleep");
        entity.AnimManager.StartAnimation(new AnimationMetaData()
        {
            Code = "idle",
            Animation = "RoosterCall",
            AnimationSpeed = 1.3f,
            Weight = 10,
            BlendMode = EnumAnimationBlendMode.AddAverage
        });
        entity.PlayEntitySound("creature/chicken/rooster-call", range: 48);

        if (IsAlarming) WakeAllPlayers();
    }

    /// <summary>
    /// Wakes up all players.
    /// </summary>
    /// Must be run server side.
    private void WakeAllPlayers()
    {
        if (_world.Api is not ICoreServerAPI sapi)
        {
            _world.Logger.Error("Attempted to get server api on server only behaviour and failed.");
            return;
        }

        var sleepMod = sapi.ModLoader.GetModSystem<ModSleeping>();
        if (sleepMod == null)
        {
            _world.Logger.Error($"Attempted to get {nameof(ModSleeping)} but failed.");
            return;
        }

        if (!sleepMod.AllSleeping) return;

        sapi.Logger.Notification("Gooooooooood Morning, Vietnam");

        IsAlarming = false;

        sleepMod.WakeAllPlayers();

        // Manually broadcast to clients since WakeAllPlayers() doesn't
        var serverChannel = sapi.Network.GetChannel("sleeping");
        serverChannel.BroadcastPacket(new NetworksMessageAllSleepMode { On = false });
    }

    public override void OnInteract(EntityAgent byEntity, ItemSlot itemslot, Vec3d hitPosition, EnumInteractMode mode,
        ref EnumHandling handled)
    {
        base.OnInteract(byEntity, itemslot, hitPosition, mode, ref handled);

        if (!(byEntity.Controls.ShiftKey && itemslot.Empty && mode == EnumInteractMode.Interact)) return;

        IsAlarming = !IsAlarming;
        handled = EnumHandling.Handled;

        if (!IsAlarming || byEntity is not EntityPlayer entityPlayer) return;
        var player = entity.World.PlayerByUid(entityPlayer.PlayerUID) as IServerPlayer;
        player?.SendLocalisedMessage(GlobalConstants.GeneralChatGroup, "cockclock:alarm-toggled-on");
    }
}