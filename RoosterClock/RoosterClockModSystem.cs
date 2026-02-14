using System;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace RoosterClock;

public class RoosterClockModSystem : ModSystem
{
    public override void Start(ICoreAPI api)
    {
        base.Start(api);
        api.RegisterEntityBehaviorClass("alarmclock", typeof(EntityBehaviorAlarmClock));
    }
}