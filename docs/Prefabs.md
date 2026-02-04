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

- Prefabs currently snapshot entity **transform**, **SpriteRenderer**, and **Animator** data.
- The `InstantiatePrefab` call returns the root entity (as defined by the `rootId` you pass into `Prefab.FromScene`).
- If you want to customize instance properties after spawn, simply edit the returned `Entity` or other entities in the scene.
