# Mutatis-Mutandis Parity Matrix

| Capability | Unity Pattern | Godot Pattern | Classification |
|---|---|---|---|
| Project scaffold | project + packages | `project.godot` + folders | NativeAlternative |
| Prefab workflow | prefab assets | `PackedScene` and instancing | NativeAlternative |
| Scene graph edits | GameObject hierarchy | node tree (`.tscn`) | DirectEquivalent |
| Script attach | MonoBehaviour attach | `script = ExtResource(...)` | DirectEquivalent |
| Asset sidecars | `.meta` | `.import` for imported assets | NativeAlternative |
| Editor commands | Unity CLI/editor hooks | Godot `--headless` CLI | NativeAlternative |
| Plugin registry | package manifest | `addons/` + `editor_plugins` | NativeAlternative |
| Unity-only systems | NavMesh/InputSystem-specific | deferred extensions | DeferredNoEquivalent |
