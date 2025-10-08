extends Area2D

@onready var victory_screen: CanvasLayer = $victoryscreen

func _ready() -> void:
	body_entered.connect(_on_body_entered)

func _on_body_entered(body: Node) -> void:
	if body.is_in_group("player"):
		victory_screen.visible = true



func _on_button_pressed() -> void:
	get_tree().change_scene_to_file("res://main_menu.tscn")
