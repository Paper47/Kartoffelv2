extends Area2D

@onready var sprite_to_change = $Sprite2D

signal player_entered(player_node)
signal vanish_request

@export var target_rigid: RigidBody2D
@export var only_first_time:= true

func _ready() -> void:
	body_entered.connect(_on_body_entered)
	
	if target_rigid:
		vanish_request.connect(target_rigid._on_vanish_request)

func _on_body_entered(body: Node2D) -> void:
	if body.name == "player":
		var new_texture = load("res://images/green_button.png")
		sprite_to_change.texture = new_texture
		player_entered.emit()
		vanish_request.emit()
		if only_first_time:
			monitoring = false
		
		
