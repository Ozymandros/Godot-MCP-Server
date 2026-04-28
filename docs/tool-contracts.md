# Tool Contracts

All tools return:

```json
{
  "success": true,
  "message": "Human readable status.",
  "data": { "optional": "kv payload" },
  "suggestedRemediation": "optional remediation"
}
```

## Core and project

- `health_check()`
- `get_server_info()`
- `get_server_capabilities()`
- `create_godot_project(projectName)`
- `get_project_info()`
- `configure_autoload(key, value, enabled)`
- `add_plugin(pluginName)`

## Scenes and nodes

- Common scene path contract for scene/node tools:
  - Effective target path is resolved as `projectPath + /scenes/ + fileName`.
  - `fileName` must end with `.tscn`.
  - Missing scene files are auto-bootstrapped with a minimal valid `.tscn` using `root_type` (default: `Node`).
- `create_scene(projectPath, fileName, rootNodeName, rootNodeType, rawContent?)`
- `add_node(projectPath, fileName, parentPath, nodeName, nodeType, root_type?)`
- `set_node_property(projectPath, fileName, nodeName, propertyKey, propertyValue, root_type?)`
- `remove_node(projectPath, fileName, nodeName, root_type?)`
- `instantiate_packed_scene(projectPath, fileName, parentPath, packedSceneFileName, instanceName, root_type?)`
- `save_branch_as_scene(projectPath, fileName, nodeName, destinationFileName, root_type?)`

## Scene graph

- Common scene path contract for `scene.*` tools:
  - Effective target path is resolved as `projectPath + /scenes/ + fileName`.
  - `fileName` must end with `.tscn`.
  - Missing scene files are auto-bootstrapped with a minimal valid `.tscn` using `root_type` (default: `Node`).
- `scene.list_nodes(projectPath, fileName, root_type?)`
  - Returns full tree entries with: `name`, `type`, `nodePath`, `parent`, `children`, `script`, `properties`.
- `scene.add_node(projectPath, fileName, parentNodePath, nodeType, nodeName, root_type?)`
- `scene.remove_node(projectPath, fileName, nodePath, root_type?)`
- `scene.move_node(projectPath, fileName, nodePath, newParentPath, root_type?)`
- `scene.rename_node(projectPath, fileName, nodePath, newName, root_type?)`
- `scene.get_node_properties(projectPath, fileName, nodePath, root_type?)`
  - Returns a dictionary of node properties.
- `scene.set_node_properties(projectPath, fileName, nodePath, properties, root_type?)`
  - Updates only provided keys.
  - Value constraints: primitive JSON values only (`string`, `number`, `boolean`).

## UI tooling

- `ui.list_controls(scenePath)`
  - Returns UI control nodes with: `name`, `type`, `nodePath`, `parent`, `properties`.
- `ui.add_control(scenePath, parentNodePath, controlType, controlName, properties?)`
- `ui.set_layout_preset(scenePath, controlNodePath, preset)`
  - Supported presets: `full_rect`, `top_left`, `center`.
- `ui.set_control_properties(scenePath, controlNodePath, properties)`
  - Value constraints: primitive JSON values only (`string`, `number`, `boolean`).

## Lighting

- `light.list(projectRootPath)`
  - Returns discovered lights with: `scenePath`, `nodePath`, `type`, `energy`, `color`, `shadowsEnabled`.
- `light.create(scenePath, parentNodePath, lightType, nodeName, preset?)`
  - Supported presets: `sun`, `fill`, `spot`.
- `light.update(scenePath, nodePath, properties)`
  - Supported properties: `light_energy`, `light_color`, `shadow_enabled`, `light_specular`.
- `light.validate(projectRootPath)`
  - Reports lint-style issues (for example: non-positive energy, extreme intensity).

## Physics

- `physics.list_bodies(projectRootPath)`
  - Returns discovered bodies with: `scenePath`, `nodePath`, `type`, `collisionLayer`, `collisionMask`, `gravityScale`, `lockRotation`.
- `physics.create_body(scenePath, parentNodePath, bodyType, nodeName, addCollisionShape?)`
- `physics.update_body(scenePath, nodePath, properties)`
  - Supported properties: `collision_layer`, `collision_mask`, `gravity_scale`, `lock_rotation`.
- `physics.validate(projectRootPath)`
  - Reports lint-style issues (for example: invalid layer/mask, missing collision shape).

## Scripts and resources

- `create_script(path, language, baseType, className)`
- `attach_script(scenePath, nodeName, scriptPath)`
- `validate_script(scriptPath, isCSharp)`
- `create_resource(path, type, properties)`

## Resource pipeline

- `resource.read(resourcePath)`
  - Returns typed payload with `type` and `properties`.
- `resource.write(resourcePath, type, properties)`
- `resource.update_properties(resourcePath, properties)`
- `resource.remove_property(resourcePath, propertyKey)`

## Import lifecycle

- `generate_import_file(assetPath, importer, type, parameters?)`
- `reimport_asset(assetPath)`
- `create_texture(texturePath)`
- `create_audio(audioPath)`

## Editor automation

- `run_editor_command(arguments)`
- `manage_export_presets(presetName, platform)`

## SDK ecosystem

- `discover_integrations()`
- `install_integration(integrationName, source, profile)`
- `enable_plugin(pluginName, enabled)`
- `verify_integration_health(integrationName)`
- `list_integration_compatibility()`

Example input:

```json
{
  "projectPath": "C:/Projects/MyGame",
  "fileName": "Main.tscn",
  "root_type": "Node2D",
  "rootNodeName": "Main",
  "rootNodeType": "Node2D"
}
```
