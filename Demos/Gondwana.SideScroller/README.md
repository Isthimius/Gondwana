# Gondwana Azure Strike

A compact side-scrolling shoot-'em-up demonstrating first-class `SceneLayer` parallax.

- Move with **WASD** or the **arrow keys**.
- Fire with **Space**.
- Restart after victory or defeat with **R**.
- Quit with **Escape**.

The camera follows the constantly advancing player on X. Four scene layers use parallax factors `0.08`, `0.22`, `0.48`, and `1.0`, so distant stars, nebulae, and near stars scroll at visibly different rates without game code manually repositioning their tiles.
