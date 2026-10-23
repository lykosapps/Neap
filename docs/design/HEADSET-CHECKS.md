# Headset checks for the redesign

What the owner and Claude check together on the real Stealth Pro II before
the redesign goes into main. The pretend headset and the screen checks have
already covered everything here that a picture or a script can show; these
are the parts only the hardware answers.

For each, prove which device answered: a transmitter answers from memory for
a headset it can no longer reach.

## Home and the menu

1. **Profile switcher:** choose each profile from the top of the menu. The
   headset takes its settings, and the switcher names the active one.
2. **Noise cancellation:** Off, Cancelling and Transparency from Home. The
   headset changes, and a press of its own Mode button moves the choice.
3. **Microphone tile:** off and on from Home, then flip the boom arm. The
   tile follows the arm.
4. **Equaliser quick switch:** choose a preset from Home's drop-down. The
   sound changes, and Equaliser settings opens the Game tab on that preset.

## Game and chat

5. **Live levels:** with a game and a chat app playing, each side's meter
   beside the dial moves with its sound. Check in dark, light and high
   contrast.
6. **Chat wheel:** turn the headset's wheel. The dial glides to its place.
7. **Levels pause:** put Neap behind another window. The meters stop, and
   start again when it comes back.

## Equaliser page

8. **Drag:** drag a band. The sound follows, and the bubble shows the value.
9. **Keyboard:** Left and Right choose a band, Up and Down move it, Shift
   moves it further. The headset follows each step.
10. **Undo and Redo:** buttons and Ctrl+Z and Ctrl+Y. The headset plays each
    step back.
11. **Compare, held:** on a factory preset, on a saved preset and on an edited
    curve, in Bands and in Parametric. Flat while held; on release the curve,
    the Edited tag and the preset Discard goes back to are all as before.
12. **Compare on a new, unsaved curve:** the case with no preset to go back
    to. On release the curve comes back with all ten bands written.
13. **Presets:** choose, save, rename and delete one of your own, and check
    the slot count.

## Mini panel

14. **Open and close:** a left-click on the tray icon opens it by the clock. A
    second click, Escape or a click elsewhere closes it, and it never flickers
    shut as it opens.
15. **Controls:** mix, microphone, noise cancellation and preset each change
    the headset, and changes made on the headset show in the panel.
16. **Tray icon:** sharp and readable at the tray's size, in a light and a
    dark taskbar.

## Look

17. **Flat look:** on your own screen, every control still reads as one with
    no outlines, and the keyboard focus ring shows on each one in turn.

## Third round

Checks 1 to 17 passed on 18 October 2026. These cover what came after.

18. **Home cards:** the Microphone and Superhuman Hearing switches change the
    headset, and the card tints while on. Flip the boom arm: the Microphone
    card says so.
19. **Superhuman Hearing sound:** faded while it is off. Turned on, Legacy,
    Footsteps and Gunshots each change the sound.
20. **Spatial sound on Home and in the mini panel:** Off, Windows Sonic and
    Dolby Atmos each take effect in Windows, and a change made in Windows'
    own settings shows here.
21. **Mini panel switch in Settings:** switched off, a click on the tray icon
    opens the window and the icon's menu has no Mini panel. Switched off while
    the panel is open, the panel closes. Switched on, both come back. The
    choice survives a restart.
22. **Profile in use:** switch profiles from the menu, and start an app that
    has a profile. The Profiles page moves its "In use" mark each time.
23. **Window top:** the menu and its edge reach the top of the window, nothing
    is written over the mark, and the window drags by the top of the menu and
    by the strip beside the status. Narrow the window: the menu button sits
    in that strip and opens the menu.

## Fourth round, for 0.3.0

What came after the third round. Checks 5 and 7 are worded for the meters,
which replaced the arcs inside the ring, and need doing again.

Checks 5, 7 and 24 to 29 passed on 23 October 2026 on the Stealth Pro II
through its Charging Dock, with the try-it copy. Check 30 needs a Linux PC
and was done only in an Ubuntu 24.04 VM (below). The same session found the chat
wheel stuck at the middle after a quick turn through it, fixed in the same
day's commit; the turn toward both ends was checked by hand afterward.

Check 30, 23 October 2026, in the VM with the Stealth Pro II Charging Dock
shared from Windows and the headset on: with Neap's rule in, Neap stayed
connected for ten minutes idle with no transmitter restart, and a change to
noise cancellation, made with the headset answering, took and was read back.
The VM kept the dock awake before the rule went in, so this shows Neap works
with the rule, not that the rule stops a PC putting the transmitter to sleep.
That still needs a real Linux PC, such as the Steam Deck. After the dock was
handed back to Windows its status ring blinked purple, with the headset
connected and sound playing, until the dock's USB cable was unplugged and
plugged in again; the headset's lights read as before throughout.

24. **Chat comes from:** open the dropdown, tick a chat app, untick it, and
    press Refresh with a new app playing. The card and the cards below it
    never move, and the dial turns on once an app is ticked.
25. **Game and chat levels:** move the dial. Each side's number beside it
    follows the mix, and the meters keep moving with the sound.
26. **Sliders:** drag each slider on Audio, click on its track, and use the
    arrow keys. The headset follows, the value beside it changes, and no
    bubble appears.
27. **Microphone, boom in:** flip the boom arm, then mute from Neap's switch.
    The card says "Muted by the boom arm" for the arm and "Muted on this PC"
    for the switch, with no tone from the headset for the switch.
28. **Microphone, boom out:** take the boom microphone out while connected,
    then mute with the headset's button and with Neap's switch. Both mute the
    headset with its tone, the card says "Muted on the headset", and putting
    the boom back in leaves nothing muted that you didn't mute.
29. **Mini panel tiles:** a press turns a tile off, and a second press puts
    it back the way it was last on; the arrow in a tile's corner lists its
    choices; the slim game and chat slider moves the mix.
30. **Linux, a transmitter kept awake:** on a Linux PC with the Stealth Pro
    II, allow Neap's access rule when it asks. Leave the headset idle for ten
    minutes, then change a setting: the headset answers without Neap
    restarting the transmitter.
