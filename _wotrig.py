import json, urllib.request

def rpc(method, params):
    body = json.dumps({"jsonrpc": "2.0", "id": 1, "method": method, "params": params}).encode()
    req = urllib.request.Request("http://127.0.0.1:6400/rpc/", data=body, headers={"Content-Type": "application/json"})
    return json.loads(urllib.request.urlopen(req, timeout=90).read().decode())

fails = []
def step(label, method, params):
    try:
        r = rpc(method, params)
        if "error" in r:
            fails.append(label)
            print(f"{label}: FAIL {r['error']['message'][:140]}")
        else:
            print(f"{label}: OK")
    except Exception as ex:
        fails.append(label)
        print(f"{label}: HTTP-FAIL {ex}")

FBX = "Assets/Vehicle/USSR/1lvl/BT-2/BT-2.fbx"

for dead in ["Probe", "TankPlayer", "TankBot"]:
    step(f"delete {dead}", "object/delete", {"path": dead})
step("untag old capsule", "object/setProperty", {"path": "Player", "component_type": "GameObject", "property": "tag", "value": "Untagged"})

def tank(name, x, z, yaw, team, driver):
    step(f"{name} instantiate", "prefab/instantiate", {"prefab_path": FBX, "name": name, "position": [x, 0, z]})
    step(f"{name} collider", "object/addComponent", {"path": name, "component_type": "BoxCollider"})
    step(f"{name} col center", "object/setProperty", {"path": name, "component_type": "BoxCollider", "property": "center", "value": [0, 1.0, 0]})
    step(f"{name} col size", "object/setProperty", {"path": name, "component_type": "BoxCollider", "property": "size", "value": [2.3, 2.2, 5.7]})
    step(f"{name} health", "object/addComponent", {"path": name, "component_type": "TankHealth"})
    step(f"{name} paint off", "object/setProperty", {"path": name, "component_type": "TankHealth", "property": "paintTeam", "value": False})
    if team != "green":
        step(f"{name} team", "object/setProperty", {"path": name, "component_type": "TankHealth", "property": "team", "value": team})
    step(f"{name} driver", "object/addComponent", {"path": name, "component_type": driver})
    step(f"{name} turret node", "object/setProperty", {"path": name, "component_type": driver, "property": "turretNode", "value": "Turret_01"})
    step(f"{name} gun node", "object/setProperty", {"path": name, "component_type": driver, "property": "gunNode", "value": "gun_04_Shape"})
    step(f"{name} muzzle", "object/create", {"name": "Muzzle", "position": [x, 1.8, z + 2.6], "parent_path": name + "/Turret_01"})
    step(f"{name} yaw", "object/setTransform", {"path": name, "rotation": [0, yaw, 0]})

tank("BT_Player", -8, 0, 70, "green", "TankController")
step("player tag", "object/setProperty", {"path": "BT_Player", "component_type": "GameObject", "property": "tag", "value": "Player"})
tank("BT_Bot", 8, 6, -110, "red", "TankAI")
step("SAVE", "scene/save", {})
print("FAILS:", fails)
