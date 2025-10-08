extends Area2D

@onready var death_screen: CanvasLayer = $failurescreen

func _ready() -> void:
	body_entered.connect(_on_body_entered)

func _on_body_entered(body: Node) -> void:
	if body.is_in_group("player"):
		death_screen.visible = true



func _on_button_pressed() -> void:
	get_tree().change_scene_to_file("res://main_menu.tscn")
