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

## Scripts and resources

- `create_script(path, language, baseType, className)`
- `attach_script(scenePath, nodeName, scriptPath)`
- `validate_script(scriptPath, isCSharp)`
- `create_resource(path, type, properties)`

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
