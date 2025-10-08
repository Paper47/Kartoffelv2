extends Control

@onready var label: Label = $Label

var elapsed := 0.0
var running := false

func _ready() -> void:
	start()

func _process(delta: float) -> void:
	if running:
		elapsed += delta
		label.text = _format_time(elapsed)

func start() -> void:
	running = true

func pause() -> void:
	running = false
	
func reset() -> void:
	elapsed = 0.0
	label.text = _format_time(elapsed)
	
func _format_time(t: float) -> String:
	var total_seconds := int(t)
	var minutes := total_seconds / 60
	var seconds := total_seconds % 60
	return "%02d:%02d" % [minutes, seconds]
