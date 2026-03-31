extends Node

# Minimal operations script used as a placeholder. Real operations should
# implement specific commands and write JSON output to stdout.

func _run_operation(operation, payload_path):
    var payload = {}
    if FileAccess.file_exists(payload_path):
        var f = FileAccess.open(payload_path, FileAccess.READ)
        payload = parse_json(f.get_as_text())
        f.close()

    print("{\"status\": \"ok\", \"operation\": " + str(operation) + "}")

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
