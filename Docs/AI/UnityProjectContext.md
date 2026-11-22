# comsys

Unity 6000.6.3f1 project using URP 2D, uGUI and the Input System. The enabled startup scene is `Assets/Scenes/SampleScene.unity`. It contains the camera, 2D light, terminal Canvas and EventSystem.

The terminal loads its serialized exercise bank from `Assets/Terminal/Exercises.json`. Question IDs are stable keys for per-question PlayerPrefs statistics. The scene contains the interface hierarchy. Runtime code grades simulated input and updates the existing controls.

The Unity Pipeline editor connection and WebGL support module were detected during implementation. The intended delivery target is Web.
