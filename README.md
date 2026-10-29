# Foxfire Arena — Hypeline Demo for Unity 6

Before opening the project, obtain the Hypeline-enabled Muxy Gateway Unity SDK and place its package folder at `HypelineSDK/` in this project, with `package.json` directly inside that folder. The SDK is not included in this snapshot. The package manifest points to that local folder; without it the project cannot compile. Use the SDK version that provides the `MuxyHypelineMenu` and `MuxyHypelineInteractionAlert` prefabs.

Open this folder in Unity Hub with Unity 6000.0.85f1. Open `Assets/Foxfire/Scenes/FoxfireArena.unity` and press Play. The [Unity 2022.3 version](https://github.com/jrslyce/foxfire-hypeline-demo) is maintained separately.

Choose Play. Move with WASD or arrow keys; attacks fire automatically. Collect drops, survive, and defeat the Warden. Press Escape to pause or resume.

The scene includes the Muxy Hypeline integration. The `HypelineSDK/` folder is ignored by Git so each developer can supply their own SDK checkout.

## Muxy Hypeline setup

The scene contains `MuxyGatewayManager`, `MuxyHypelineMenu`, and `MuxyHypelineInteractionAlert`. Both Hypeline prefabs reference the same manager. Set the manager's **Game ID** to the exact ID linked in the broadcaster's Muxy Gateway Twitch extension; the field is intentionally blank until that ID is verified. Import **Window > TextMeshPro > Import TMP Essential Resources** if Unity asks for them.

The manager lists the Foxfire viewer actions. `HypelineGateway` forwards received actions to the game's existing dispatcher, which applies gameplay effects and accepts or refunds the transaction according to the result. Actions are unavailable while the start menu or pause menu is open. The alert prefab displays interactions; it does not implement their effects.

For a Twitch test, connect the broadcaster through the Hypeline menu, enter the forest, and send an available action from the linked extension. Check both the in-game effect and its transaction result. Keep connection PINs out of recordings and screenshots. The SDK checkout labels TikTok support as coming soon; it cannot currently connect TikTok.

If the manager is removed from the scene, the demo falls back to its local interaction simulator.

## Editing the start menu

Choose **Enter the Forest** to move the fox yourself, or **AI Mode** to let the fox navigate and fight autonomously. Escape pauses either mode; resuming keeps the selected mode. After a run, either mode can be chosen again.

Open the FoxfireArena scene and expand **Start Menu > Menu Panel** in the Hierarchy. Select the Title, Subtitle, Fox Portrait, Play Button, and control labels to edit their components in the Inspector. The menu uses Unity UI and the built-in LegacyRuntime font. The Start Menu component has the alternate pause and results text. The Play Button’s On Click event is connected to Start Menu; keep that connection when editing the button.

The AI Mode button is created from the Play Button at runtime.
