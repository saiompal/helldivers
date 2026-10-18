Hat Hop — four-level main menu selection

Stop Unity Play Mode. Copy the included Assets folder into the Unity project
root, merge folders and replace:

Assets/Scenes/MainMenu.unity
Assets/Editor/MainMenuBuilder.cs

Keep your existing .meta files. Let Unity finish importing and compiling.
Reopen MainMenu.unity if it was already open, then press Play. No rebuild or
Inspector setup is needed.

The menu contains four matching buttons in the same row:
Level 0 loads Level0; Level 1 loads Level1; Level 2 loads Level2; and Level 3
loads the Ring Exchange scene. Title, colors, instructions and Quit retain the
existing menu style. Keyboard/controller navigation starts on Level 0. The
previous gameplay/light/conveyor patches are not overwritten.

The editor builder recreates these buttons if you rebuild the menu later. On
script import it enables/orders the scenes as MainMenu, Level0, Level1, Level2,
Level3 while preserving other valid scene entries and input configuration.

Validation: menu scene file references, both click-event targets, existing
button styling and archive integrity checked. Unity Editor rendering,
compilation and clicking through the menu remain unverified here.
