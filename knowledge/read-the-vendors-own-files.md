---
name: read-the-vendors-own-files
description: "When reverse engineering, read the vendor software's own config and state files early — they name the structures that captures only show anonymously"
metadata: 
  node_type: memory
  type: feedback
  originSessionId: 69317492-c616-4797-9d17-41d8585ca95f
  modified: 2026-09-21T12:25:19.648Z
---

When reverse engineering a device or protocol, read the vendor
application's own settings, cache and state files **early**, alongside
traffic capture. The owner suggested this after I had spent hours on captures:
"Can you just read how Swarm does it? That's been proven an effective
method thus far."

He was right, and it was decisive within minutes. Swarm II's profile store
(`%APPDATA%\Turtle Beach\Swarm II\Setting\STEALTH_PRO_II_PROFILE_Mgr.dat`,
a Qt-serialised snapshot) listed `CG1`..`CG5` and `CM1`..`CM5` as protocol
*categories* with their own key ranges. That is what revealed the headset
could list its own equaliser presets — something I had recorded as
impossible after reading captures alone.

**Why:** a traffic capture shows bytes going past. The vendor's own files
show the *model* the vendor holds — the names, the groupings, the ranges,
the things it expects to exist. Captures tell you what happened once; the
config tells you the shape of the world the software believes in, including
features that were not exercised while you were watching.

**How to apply:** near the start of a reverse-engineering task, enumerate
the vendor app's data directories and dump strings from anything that looks
like state (`.ini`, `.dat`, `.json`, registry keys, Qt `@ByteArray` blobs —
Qt strings are UTF-16BE, so search for those too). Compare the names found
there against what the captures show. Disagreements are the interesting
part: a name the app knows but never sends is a feature you have not
triggered; a name it knows that you decided "does not exist" means your
decoder is wrong.

Related: [[parsers-must-not-drop-silently]]
