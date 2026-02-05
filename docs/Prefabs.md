# Prefabs (2D)

This guide shows how to create, save, load, and instantiate prefabs using the new `Prefab` and `PrefabJson` APIs.

## 1) Create a prefab from a scene

Pick an entity in your scene to be the root of the prefab (the entity that will be returned when you instantiate it later).

```csharp
using Engine.Core.Scene;

// `scene` is your current Scene instance.
var rootEntity = scene.FindByName("Player");

if (rootEntity is null)
{
    throw new InvalidOperationException("Player entity not found.");
}

Prefab prefab = Prefab.FromScene(scene, rootEntity.Id);
```

## 2) Serialize the prefab to JSON

```csharp
using Engine.Core.Serialization;

string json = PrefabJson.Serialize(prefab);
File.WriteAllText("Content/Prefabs/Player.prefab.json", json);
```

## 3) Load a prefab from JSON

```csharp
using Engine.Core.Serialization;
using Engine.Core.Scene;

string json = File.ReadAllText("Content/Prefabs/Player.prefab.json");
Prefab prefab = PrefabJson.Deserialize(json);
```

## 4) Instantiate the prefab into a scene

```csharp
using System.Numerics;
using Engine.Core.Scene;

// Spawn at exact position from the prefab.
Entity instance = scene.InstantiatePrefab(prefab);

// Or override its position (offsets the prefab so the root lands at the new position).
Entity movedInstance = scene.InstantiatePrefab(prefab, new Vector3(5f, 2f, 0f));
```

## Notes

- Prefabs currently snapshot entity **transform**, **SpriteRenderer**, **Animator**, **BoxCollider2D**, **PhysicsBody2D**, and **Rigidbody2D** data.
- The `InstantiatePrefab` call returns the root entity (as defined by the `rootId` you pass into `Prefab.FromScene`).
- If you want to customize instance properties after spawn, simply edit the returned `Entity` or other entities in the scene.

## Prefab JSON Fields

This is the full list of fields supported by `*.prefab.json` files. All fields are optional unless stated otherwise.

Top-level:
- `Version` (required, int): Prefab JSON version. Current value is `1`.
- `RootId` (required, string GUID): Which entity in `Entities` is the root.
- `Entities` (required, array): List of prefab entities.

Entity:
- `Id` (required, string GUID): Unique ID for this prefab entity (local to the prefab).
- `Name` (required, string): Entity name.
- `Transform` (required, object): Transform data.
- `SpriteRenderer` (optional, object): SpriteRenderer component data.
- `Animator` (optional, object): Animator component data.
- `BoxCollider2D` (optional, object): BoxCollider2D component data.
- `PhysicsBody2D` (optional, object): PhysicsBody2D component data.
- `Rigidbody2D` (optional, object): Rigidbody2D component data.
- `DebugRender2D` (optional, object): Debug render flags.

Transform:
- `Position` (required, array of 3 floats): `[x, y, z]`.
- `RotationZDegrees` (optional, float): Z rotation in degrees. Preferred field.
- `RotationZRadians` (optional, float): Z rotation in radians (legacy; still supported for backwards compatibility).
- `Scale` (required, array of 3 floats): `[x, y, z]`.

SpriteRenderer:
- `SpriteId` (required, string): Sprite key from the atlas.
- `Tint` (optional, array of 4 floats): `[r, g, b, a]`, defaults to white.
- `Layer` (optional, int): Render order.
- `OverrideSourceRect` (optional, bool): Use `SourceRect` instead of sprite default.
- `SourceRect` (optional, array of 4 ints): `[x, y, w, h]`.
- `OverridePixelsPerUnit` (optional, bool): Use `PixelsPerUnitOverride` instead of sprite default.
- `PixelsPerUnitOverride` (optional, float): Custom PPU.
- `Flip` (optional, string): `None`, `X`, or `Y`.

Animator:
- `ControllerId` (required, string): Animator controller id.
- `ClipId` (optional, string): Current clip id (can be blank).
- `Playing` (optional, bool)
- `Speed` (optional, float)
- `LoopOverride` (optional, bool)
- `Loop` (optional, bool)
- `FrameIndex` (optional, int)
- `TimeIntoFrame` (optional, float)
- `DefaultCrossFadeSeconds` (optional, float)
- `DefaultFreezeDuringCrossFade` (optional, bool)

BoxCollider2D:
- `Size` (required, array of 2 floats): `[width, height]`.
- `Offset` (optional, array of 2 floats): `[x, y]`.
- `IsTrigger` (optional, bool)

PhysicsBody2D:
- `IsStatic` (optional, bool): If true, this body will not be moved by collision resolution.

Rigidbody2D:
- `Mass` (optional, float)
- `Velocity` (optional, array of 2 floats): `[x, y]`.
- `UseGravity` (optional, bool)
- `GravityScale` (optional, float)
- `LinearDrag` (optional, float)
- `Friction` (optional, float): Used by collision resolution for sliding.

DebugRender2D:
- `ShowCollider` (optional, bool): Toggle collider visualization per-entity.

Example:
```json
{
  "Version": 1,
  "RootId": "11111111-1111-1111-1111-111111111111",
  "Entities": [
    {
      "Id": "11111111-1111-1111-1111-111111111111",
      "Name": "Player",
      "Transform": {
        "Position": [0, 0, 0],
        "RotationZDegrees": 30,
        "Scale": [5, 5, 1]
      },
      "SpriteRenderer": {
        "SpriteId": "player_idle_0",
        "Tint": [1, 1, 1, 1],
        "Layer": 100,
        "OverrideSourceRect": false,
        "SourceRect": [0, 0, 0, 0],
        "OverridePixelsPerUnit": false,
        "PixelsPerUnitOverride": 100,
        "Flip": "None"
      },
      "Animator": {
        "ControllerId": "player",
        "ClipId": "",
        "Playing": true,
        "Speed": 1,
        "LoopOverride": false,
        "Loop": true,
        "FrameIndex": 0,
        "TimeIntoFrame": 0,
        "DefaultCrossFadeSeconds": 0,
        "DefaultFreezeDuringCrossFade": false
      },
      "BoxCollider2D": {
        "Size": [0.16, 0.46],
        "Offset": [0, 0.089],
        "IsTrigger": false
      },
      "PhysicsBody2D": {
        "IsStatic": false
      },
      "Rigidbody2D": {
        "Mass": 1,
        "Velocity": [0, 0],
        "UseGravity": true,
        "GravityScale": 1,
        "LinearDrag": 0,
        "Friction": 0.2
      },
      "DebugRender2D": {
        "ShowCollider": true
      }
    }
  ]
}
```

## Scene Overrides (Prefab Instances)

Scenes can reference a prefab using `prefabId`. Any component present in the scene **overrides** the prefab; missing components are **filled from the prefab**.

Rules:
- If `prefabId` is set and a component is **omitted**, the prefab component is used.
- If a component is **present in the scene**, it replaces the prefab component.
- If `transform` is **omitted**, the prefab root transform is used.
- If `transform` is **present**, it overrides the prefab transform.
- `PhysicsBody2D` and `Rigidbody2D` follow the same override rules.

Minimal instance (uses prefab data):
```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Player",
  "prefabId": "Player"
}
```

Instance with overrides:
```json
{
  "id": "11111111-1111-1111-1111-111111111111",
  "name": "Player",
  "prefabId": "Player",
  "transform": {
    "position": [2, 0, 0],
    "rotationZDegrees": 0,
    "scale": [5, 5, 1]
  },
  "boxCollider2D": {
    "size": [0.3, 0.6],
    "offset": [0, 0],
    "isTrigger": false
  }
}
```
