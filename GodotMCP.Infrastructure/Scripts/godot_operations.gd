extends Node

# Minimal operations script used as a placeholder. Real operations should
# implement specific commands and write JSON output to stdout.

func _make_response(success: bool, request_id: String, message: String, data = null, error = null) -> void:
    var resp = {
        "schemaVersion": "1.0",
        "requestId": request_id,
        "success": success,
        "message": message,
        "data": data,
        "error": error
    }
    print(to_json(resp))


func _load_payload(payload_path: String) -> Dictionary:
    var payload = {}
    if FileAccess.file_exists(payload_path):
        var f = FileAccess.open(payload_path, FileAccess.READ)
        payload = parse_json(f.get_as_text())
        f.close()
    return payload


func _op_create_scene(payload_path: String) -> Dictionary:
    var payload = _load_payload(payload_path)
    var request_id = payload.get("requestId", "")
    var scene_path = payload.get("payload", {}).get("scenePath", "")
    var root = payload.get("payload", {}).get("root", {})
    if scene_path == "":
        return {"success": false, "requestId": request_id, "message": "scenePath required"}

    var root_name = root.get("name", "Root")
    var root_type = root.get("type", "Node")

    # Create a new scene with a root node and save via PackedScene
    var root_node = null
    if ClassDB.class_exists(root_type):
        root_node = load(root_type).new()
    else:
        # fallback to Node
        root_node = Node.new()
    root_node.name = root_name

    var packed = PackedScene.new()
    var ok = packed.pack(root_node)
    if ok != OK:
        return {"success": false, "requestId": request_id, "message": "Failed to pack scene"}

    var fs = File.new()
    var abs = ProjectSettings.globalize_path(scene_path)
    var dir = abs.get_base_dir()
    var d = Directory.new()
    if not d.dir_exists(dir):
        d.make_dir_recursive(dir)

    var save_err = ResourceSaver.save(abs, packed)
    if save_err != OK:
        return {"success": false, "requestId": request_id, "message": "Failed to save scene", "data": {"code": str(save_err)}}

    return {"success": true, "requestId": request_id, "message": "Scene saved", "data": {"scenePath": scene_path}}


func _op_add_node(payload_path: String) -> Dictionary:
    var payload = _load_payload(payload_path)
    var request_id = payload.get("requestId", "")
    var scene_path = payload.get("payload", {}).get("scenePath", "")
    var parent_path = payload.get("payload", {}).get("parentPath", ".")
    var node = payload.get("payload", {}).get("node", {})
    if scene_path == "":
        return {"success": false, "requestId": request_id, "message": "scenePath required"}

    var abs = ProjectSettings.globalize_path(scene_path)
    if not FileAccess.file_exists(abs):
        return {"success": false, "requestId": request_id, "message": "Scene not found"}

    var scene_res = ResourceLoader.load(abs)
    if scene_res == null:
        return {"success": false, "requestId": request_id, "message": "Failed to load scene"}

    var inst = scene_res.instance()
    if inst == null:
        return {"success": false, "requestId": request_id, "message": "Failed to instance scene"}

    var parent = inst if parent_path == "." else inst.get_node(parent_path)
    if parent == null:
        return {"success": false, "requestId": request_id, "message": "Parent node not found"}

    var node_name = node.get("name", "NewNode")
    var node_type = node.get("type", "Node")
    var new_node = null
    if ClassDB.class_exists(node_type):
        new_node = load(node_type).new()
    else:
        new_node = Node.new()
    new_node.name = node_name
    parent.add_child(new_node)

    # Resave scene
    var packed = PackedScene.new()
    var pack_ok = packed.pack(inst)
    if pack_ok != OK:
        return {"success": false, "requestId": request_id, "message": "Failed to pack scene after modification"}

    var save_err = ResourceSaver.save(abs, packed)
    if save_err != OK:
        return {"success": false, "requestId": request_id, "message": "Failed to save scene", "data": {"code": str(save_err)}}

    return {"success": true, "requestId": request_id, "message": "Node added", "data": {"scenePath": scene_path, "nodeName": node_name}}


func _op_attach_script(payload_path: String) -> Dictionary:
    var payload = _load_payload(payload_path)
    var request_id = payload.get("requestId", "")
    var scene_path = payload.get("payload", {}).get("scenePath", "")
    var node_name = payload.get("payload", {}).get("nodeName", "")
    var script_path = payload.get("payload", {}).get("scriptPath", "")

    if scene_path == "" or node_name == "" or script_path == "":
        return {"success": false, "requestId": request_id, "message": "scenePath, nodeName and scriptPath are required"}

    var abs = ProjectSettings.globalize_path(scene_path)
    if not FileAccess.file_exists(abs):
        return {"success": false, "requestId": request_id, "message": "Scene not found"}

    var scene_res = ResourceLoader.load(abs)
    if scene_res == null:
        return {"success": false, "requestId": request_id, "message": "Failed to load scene"}

    var inst = scene_res.instance()
    if inst == null:
        return {"success": false, "requestId": request_id, "message": "Failed to instance scene"}

    var target = inst.find_node(node_name, true, false)
    if target == null:
        return {"success": false, "requestId": request_id, "message": "Node not found"}

    var script = ResourceLoader.load(script_path)
    if script == null:
        return {"success": false, "requestId": request_id, "message": "Script resource not found"}

    # Attach script
    target.set_script(script)

    # Resave scene
    var packed = PackedScene.new()
    if packed.pack(inst) != OK:
        return {"success": false, "requestId": request_id, "message": "Failed to pack scene after attaching script"}

    var save_err = ResourceSaver.save(abs, packed)
    if save_err != OK:
        return {"success": false, "requestId": request_id, "message": "Failed to save scene", "data": {"code": str(save_err)}}

    return {"success": true, "requestId": request_id, "message": "Script attached", "data": {"scenePath": scene_path, "nodeName": node_name, "script": script_path}}


func _op_update_uids(payload_path: String) -> Dictionary:
    var payload = _load_payload(payload_path)
    var request_id = payload.get("requestId", "")
    var paths = payload.get("payload", {}).get("paths", [])
    if typeof(paths) != TYPE_ARRAY:
        paths = [ payload.get("payload", {}).get("path", "") ]

    var results = []
    for p in paths:
        if p == null or p == "":
            continue
        var abs = ProjectSettings.globalize_path(p)
        if not FileAccess.file_exists(abs):
            results.append({"path": p, "status": "missing"})
            continue
        var res = ResourceLoader.load(abs)
        if res == null:
            results.append({"path": p, "status": "load_failed"})
            continue
        # Try to resave to refresh uids/references
        var save_err = ResourceSaver.save(abs, res)
        if save_err != OK:
            results.append({"path": p, "status": "save_failed", "code": str(save_err)})
        else:
            results.append({"path": p, "status": "updated"})

    return {"success": true, "requestId": request_id, "message": "UID update completed", "data": {"results": results}}


func _op_reimport_asset(payload_path: String) -> Dictionary:
    var payload = _load_payload(payload_path)
    var request_id = payload.get("requestId", "")
    var asset = payload.get("payload", {}).get("assetPath", "")
    if asset == "":
        return {"success": false, "requestId": request_id, "message": "assetPath required"}

    var abs = ProjectSettings.globalize_path(asset)
    if not FileAccess.file_exists(abs):
        return {"success": false, "requestId": request_id, "message": "Asset not found"}

    # Best-effort reimport: attempt to load and resave resource which may trigger importer effects
    var res = ResourceLoader.load(abs)
    if res == null:
        return {"success": false, "requestId": request_id, "message": "Failed to load asset"}

    var save_err = ResourceSaver.save(abs, res)
    if save_err != OK:
        return {"success": false, "requestId": request_id, "message": "Reimport/save failed", "data": {"code": str(save_err)}}

    return {"success": true, "requestId": request_id, "message": "Asset reimported (best-effort)", "data": {"assetPath": asset}}

func _ready():
    # When invoked with --script, Godot passes command-line args after the
    # script path which we can access via OS.get_cmdline_args(). We'll expect
    # <operation> <payloadPath>.
    var args = OS.get_cmdline_args()
    if args.size() >= 2:
        var op = args[0]
        var payload = args[1]
        _run_operation(op, payload)
    get_tree().quit()


func _run_operation(operation: String, payload_path: String) -> void:
    var result = null
    var request_id = ""
    var message = ""
    var data = null
    var error = null
    var success = false

    # Dispatch operation and catch errors to ensure we always emit a JSON envelope.
    var ok = true
    match operation:
        "create_scene":
            result = _op_create_scene(payload_path)
        "add_node":
            result = _op_add_node(payload_path)
        "attach_script":
            result = _op_attach_script(payload_path)
        "update_uids":
            result = _op_update_uids(payload_path)
        "reimport_asset":
            result = _op_reimport_asset(payload_path)
        _:
            result = {"success": false, "requestId": "", "message": "unknown operation"}

    # Normalize result and emit
    if typeof(result) == TYPE_DICTIONARY:
        request_id = result.get("requestId", "")
        success = result.get("success", false)
        message = result.get("message", "")
        data = result.get("data", null)
        error = result.get("error", null)
    else:
        success = false
        message = "invalid operation result"

    # Emit canonical response on stdout
    _make_response(success, request_id, message, data, error)
