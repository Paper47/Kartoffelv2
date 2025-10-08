extends Control

@onready var start_btn = $CenterContainer/MarginContainer/VBoxContainer/Start
@onready var how_to_play_btn = $CenterContainer/MarginContainer/VBoxContainer/how_to_play

const GAME_SCENE_PATH := "res://main.tscn"

func _ready() -> void:
	start_btn.pressed.connect(_on_start_pressed)
	if how_to_play_btn:
		how_to_play_btn.pressed.connect(_on_how_to_pressed)
	start_btn.grab_focus()

func _on_start_pressed() -> void:
	get_tree().change_scene_to_file(GAME_SCENE_PATH)

	
func _on_how_to_pressed() -> void:
	get_tree().change_scene_to_file("res://how_to_play.tscn")
