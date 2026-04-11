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

- `create_scene(scenePath, rootNodeName, rootNodeType)`
- `add_node(scenePath, parentPath, nodeName, nodeType)`
- `set_node_property(scenePath, nodeName, propertyKey, propertyValue)`
- `remove_node(scenePath, nodeName)`
- `instantiate_packed_scene(targetScenePath, parentPath, packedScenePath, instanceName)`
- `save_branch_as_scene(sourceScenePath, nodeName, destinationScenePath)`

## Scene graph

- `scene.list_nodes(scenePath)`
  - Returns full tree entries with: `name`, `type`, `nodePath`, `parent`, `children`, `script`, `properties`.
- `scene.add_node(scenePath, parentNodePath, nodeType, nodeName)`
- `scene.remove_node(scenePath, nodePath)`
- `scene.move_node(scenePath, nodePath, newParentPath)`
- `scene.rename_node(scenePath, nodePath, newName)`
- `scene.get_node_properties(scenePath, nodePath)`
  - Returns a dictionary of node properties.
- `scene.set_node_properties(scenePath, nodePath, properties)`
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
  "scenePath": "res://scenes/Main.tscn",
  "rootNodeName": "Main",
  "rootNodeType": "Node2D"
}
```
