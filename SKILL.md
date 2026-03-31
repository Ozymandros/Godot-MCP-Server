# Godot MCP Server Skill Reference

## Core Tools

- `CreateGodotProjectAsync(projectName)`
- `GetProjectInfoAsync()`
- `ConfigureAutoloadAsync(key, value, enabled)`
- `AddPluginAsync(pluginName)`

## Scene and Node Tools

- `CreateSceneAsync(scenePath, rootNodeName, rootNodeType)`
- `AddNodeAsync(scenePath, parentPath, nodeName, nodeType)`
- `SetNodePropertyAsync(scenePath, nodeName, propertyKey, propertyValue)`
- `RemoveNodeAsync(scenePath, nodeName)`
- `InstantiatePackedSceneAsync(targetScenePath, parentPath, packedScenePath, instanceName)`
- `SaveBranchAsSceneAsync(sourceScenePath, nodeName, destinationScenePath)`

## Script Tools

- `CreateScriptAsync(path, language, baseType, className)`
- `AttachScriptAsync(scenePath, nodeName, scriptPath)`
- `ValidateScriptAsync(scriptPath, isCSharp)`

## Resource and Import Tools

- `CreateResourceAsync(path, type, properties)`
- `GenerateImportFileAsync(assetPath, importer, type, parameters)`
- `ReimportAssetAsync(assetPath)`
- `CreateTextureAsync(texturePath)`
- `CreateAudioAsync(audioPath)`

## Editor Automation

- `RunEditorCommandAsync(arguments)`
- `ManageExportPresetsAsync(presetName, platform)`

## SDK Ecosystem Tools

- `DiscoverIntegrations()`
- `InstallIntegrationAsync(integrationName, source, profile)`
- `EnablePluginAsync(pluginName, enabled)`
- `VerifyIntegrationHealth(integrationName)`
- `ListIntegrationCompatibility()`

## Example

1. Create project.
2. Create scene.
3. Add node.
4. Create and attach script.
5. Generate import files for assets.
