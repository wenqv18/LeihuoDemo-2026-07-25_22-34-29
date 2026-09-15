"""Create Spot Light 2D children under Lamp objects in Level_02/03."""

import json
import sys

from unity_mcp import UnityMcp


NORMAL_COLOR = (0.5291324257850647, 0.6163521409034729, 0.5656072497367859)
SPECIAL_COLORS = {
    "Lamp": (0.0, 1.0, 0.14958292245864868),
    "Lamp (1)": (0.03644561767578125, 1.0, 0.0),
    "Lamp (2)": (0.09411368519067764, 1.0, 0.003921568393707275),
    "Lamp (3)": (0.23766475915908813, 1.0, 0.0),
    "Lamp (4)": (0.15, 1.0, 0.03),
    "Lamp (5)": (0.05, 1.0, 0.06),
}


def light_properties(color):
    return {
        "lightType": 3,
        "intensity": 2.0,
        "color": {"r": color[0], "g": color[1], "b": color[2], "a": 1.0},
        "falloffIntensity": 0.65,
        "pointLightInnerAngle": 45.0,
        "pointLightOuterAngle": 120.0,
        "pointLightInnerRadius": 1.0,
        "pointLightOuterRadius": 4.5,
        "pointLightDistance": 3.0,
        "pointLightQuality": 2,
        "shadowsEnabled": True,
        "shadowIntensity": 0.75,
        "shadowSoftness": 0.3,
        "normalMapDistance": 3.0,
        "normalMapQuality": 2,
    }


def create_light(client, lamp_id, color):
    return client.call_json(
        "manage_gameobject",
        {
            "action": "create",
            "name": "Spot Light 2D",
            "parent": str(lamp_id),
            "layer": "InteractTrigger",
            "components_to_add": ["Light2D"],
        },
    )


def set_light_properties(client, light_id, color):
    return client.call_json(
        "manage_components",
        {
            "action": "set_property",
            "target": light_id,
            "component_type": "Light2D",
            "properties": light_properties(color),
        },
    )


def set_light_transform(client, light_id, local_y):
    return client.call_json(
        "manage_gameobject",
        {
            "action": "modify",
            "target": str(light_id),
            "search_method": "by_id",
            "position": [0.0, local_y, 0.0],
            "rotation": [0.0, 0.0, 180.0],
            "scale": [1.0, 1.0, 1.0],
            "world_space": False,
        },
    )


def deploy(client, lamp_id, color, local_y):
    create_result = create_light(client, lamp_id, color)
    if not create_result.get("success"):
        print("CREATE FAILED:", json.dumps(create_result, ensure_ascii=False))
        return None
    light_id = create_result["data"]["instanceID"]
    print(f"  created light {light_id} under {lamp_id}")
    prop_result = set_light_properties(client, light_id, color)
    if not prop_result.get("success"):
        print("  PROP WARN:", json.dumps(prop_result, ensure_ascii=False)[:600])
    transform_result = set_light_transform(client, light_id, local_y)
    if not transform_result.get("success"):
        print("  TRANSFORM FAILED:", json.dumps(transform_result, ensure_ascii=False)[:600])
    return light_id


def add_flicker(client, light_id):
    result = client.call_json(
        "manage_components",
        {"action": "add", "target": light_id, "component_type": "LampFlickerController"},
    )
    if not result.get("success"):
        print("  FLICKER FAILED:", json.dumps(result, ensure_ascii=False)[:600])
    return result


def main():
    client = UnityMcp()
    jobs = json.loads(sys.argv[1])  # list of {"lamp": id, "kind": "normal"|"special", "local_y": float, "special_variant": "Lamp"|...}
    for job in jobs:
        kind = job["kind"]
        if kind == "normal":
            color = NORMAL_COLOR
        else:
            color = SPECIAL_COLORS[job.get("special_variant", "Lamp")]
        light_id = deploy(client, int(job["lamp"]), color, float(job.get("local_y", 0.0)))
        if light_id is not None and job.get("flicker"):
            add_flicker(client, light_id)


if __name__ == "__main__":
    main()
