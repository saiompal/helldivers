Hat Hop — main menu level selection

Stop Unity Play Mode. Copy the included Assets folder into the Unity project
root, merge folders and replace:

Assets/Scenes/MainMenu.unity
Assets/Editor/MainMenuBuilder.cs

Keep your existing .meta files. Let Unity finish importing and compiling.
Reopen MainMenu.unity if it was already open, then press Play. No rebuild or
Inspector setup is needed.

The original Play button is replaced by two matching buttons in the same row:
Level 1 loads Level1; Level 2 loads Level2. Title, colors, instructions and Quit
retain the existing menu style. Keyboard/controller navigation still starts
on Level 1. The previous gameplay/light/conveyor patches are not overwritten.

The replacement editor builder also preserves these buttons if you rebuild
the menu later. On script import it enables/orders the existing scenes as
MainMenu, Level1, Level2 while preserving other valid scene entries and input
configuration. This requires the existing Level1.unity and Level2.unity files
in Assets/Scenes; this patch does not include or recreate either level.

Validation: menu scene file references, both click-event targets, existing
button styling and archive integrity checked. Unity Editor rendering,
compilation and clicking through the menu remain unverified here.
