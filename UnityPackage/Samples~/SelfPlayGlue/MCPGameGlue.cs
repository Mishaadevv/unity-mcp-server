// Project-side glue: registers game-specific MCP methods on the generic bridge.
// UPM packages cannot reference project code, so Tank* methods live here
// (Assembly-CSharp-Editor sees both the bridge package and game classes).
using System.Collections.Generic;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityMCP.Editor;

[InitializeOnLoad]
internal static class MCPGameGlue
{
    static MCPGameGlue()
    {
        UnityMCPBridge.RegisterHandler("input/set", OnInputSet);
        UnityMCPBridge.RegisterHandler("input/clear", _ =>
        {
            TankMCPInput.Clear();
            return new Dictionary<string, object> { ["override"] = false };
        });
        UnityMCPBridge.RegisterHandler("game/state", _ => GameState());
    }

    private static object OnInputSet(JObject p)
    {
        TankMCPInput.Set(OptFloat(p, "throttle"), OptFloat(p, "steer"),
            p["fire"]?.ToObject<bool>() ?? false);
        return new Dictionary<string, object>
        {
            ["override"] = true,
            ["throttle"] = TankMCPInput.throttle,
            ["steer"] = TankMCPInput.steer,
        };
    }

    private static float OptFloat(JObject p, string key)
    {
        var t = p[key];
        return t == null || t.Type == JTokenType.Null ? 0f : t.ToObject<float>();
    }

    // One-call snapshot for AI self-play: every tank's team/HP/pose/gun state.
    private static object GameState()
    {
        var tanks = new List<object>();
        foreach (var th in TankHealth.all)
        {
            if (th == null) continue;
            var t = th.transform;
            float reload = 1f, turretYaw = t.eulerAngles.y;
            var pc = th.GetComponent<TankController>();
            var ai = th.GetComponent<TankAI>();
            if (pc != null) { reload = pc.ReloadFrac(); turretYaw = pc.TurretYaw(); }
            else if (ai != null) { reload = ai.ReloadFrac(); turretYaw = ai.TurretYaw(); }
            tanks.Add(new Dictionary<string, object>
            {
                ["name"] = th.name,
                ["team"] = th.team,
                ["hp"] = th.hp,
                ["maxHP"] = th.maxHP,
                ["alive"] = th.Alive,
                ["position"] = new[] { t.position.x, t.position.y, t.position.z },
                ["yaw"] = t.eulerAngles.y,
                ["turretYaw"] = turretYaw,
                ["reloadFrac"] = reload,
            });
        }
        return new Dictionary<string, object> { ["tanks"] = tanks };
    }
}
