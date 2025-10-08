extends RigidBody2D

func _on_vanish_request() -> void:
	freeze = true
	collision_layer = 0
	_disable_all_shapes(self)
	_hide_visuals_recursive(self)

func _disable_all_shapes(node: Node) -> void:
	for child in node.get_children():
		if child is CollisionShape2D:
			child.disabled = true
		_disable_all_shapes(child)
		
func _hide_visuals_recursive(node: Node) -> void:
	for child in node.get_children():
		if child is CanvasItem:
			child.visible = false
		_hide_visuals_recursive(child)
			
	
	
