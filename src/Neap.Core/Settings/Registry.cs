namespace Neap.Core.Settings;

/// <summary>The headset settings confirmed by capture.</summary>
/// <remarks>
/// Every entry was observed being written or reported by Swarm II while a
/// specific control was operated by hand. A value that is read back but not
/// yet matched to a control is named with the best hypothesis, left
/// non-writable, and says so in its note. The exceptions are 0x730 and 0x740,
/// confirmed by ear rather than by capture; see the comment above them.
/// </remarks>
public static class Registry
{
    /// <summary>The Mode button's functions, zero-based.</summary>
    /// <remarks>Confirmed: while pressing the button toggled ANC, its value was 0.</remarks>
    public static readonly IReadOnlyDictionary<int, string> ModeButtonOptions =
        new Dictionary<int, string>
        {
            [0] = "Noise cancellation on/off",
            [1] = "Next equaliser preset",
            [2] = "Noise gate on/off",
        };

    /// <summary>The dial's functions, one-based unlike the Mode button's.</summary>
    /// <remarks>
    /// Value 0 has never been observed. Setting the dial back to its factory
    /// default (Mic monitoring) writes 1, and choosing Treble then Bass writes
    /// 4 then 3.
    /// </remarks>
    public static readonly IReadOnlyDictionary<int, string> DialOptions =
        new Dictionary<int, string>
        {
            [1] = "Mic monitoring volume",
            [2] = "Game and chat mix",
            [3] = "Bass boost level",
            [4] = "Treble boost level",
            [5] = "Noise gate volume",
        };

    private static readonly IReadOnlyDictionary<int, string> ShhPresetOptions =
        new Dictionary<int, string> { [0] = "Legacy", [1] = "Footsteps", [2] = "Gunshots" };

    private static readonly IReadOnlyDictionary<int, string> AutoShutoffOptions =
        new Dictionary<int, string>
        {
            [0] = "Off",
            [1] = "5 minutes",
            [2] = "10 minutes",
            [3] = "20 minutes",
            [4] = "30 minutes",
        };

    public static readonly IReadOnlyList<SettingKey> All = Build();

    public static readonly IReadOnlyDictionary<int, SettingKey> ByKey =
        All.ToDictionary(k => k.Key);

    public static readonly IReadOnlyDictionary<string, SettingKey> ByName =
        All.ToDictionary(k => k.Name, StringComparer.Ordinal);

    /// <summary>Finds a setting by name, numeric key, or "0x…" string.</summary>
    public static SettingKey Resolve(object key)
    {
        switch (key)
        {
            case SettingKey found: return found;
            case int number when ByKey.TryGetValue(number, out var byNumber): return byNumber;
            case int number: throw new KeyNotFoundException($"unknown setting 0x{number:x}");
        }
        var text = key.ToString() ?? "";
        if (ByName.TryGetValue(text, out var byName)) return byName;
        var trimmed = text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text;
        if (int.TryParse(trimmed, System.Globalization.NumberStyles.HexNumber,
                         null, out int parsed) && ByKey.TryGetValue(parsed, out var byHex))
            return byHex;
        throw new KeyNotFoundException($"unknown setting '{text}'");
    }

    /// <summary>The value to send for a write, checked against what is confirmed.</summary>
    /// <remarks>
    /// Only a writable key in this registry, within its range, is sent. The one
    /// addition is the lighting in the other transmitter slots: each slot's two
    /// brightnesses sit at +1 and +2 in its own block, and only slot one's are
    /// listed. A slot's base address is never written: writing it moves the
    /// headset onto a transmitter nobody chose.
    /// </remarks>
    /// <exception cref="ArgumentException">The write is not a confirmed one.</exception>
    public static string WireValue(int key, object value)
    {
        if (ByKey.TryGetValue(key, out var known) && known.Writable) return known.Validate(value);

        int inSlot = (key - TransmitterBlock) % TransmitterStride;
        if (key is >= TransmitterBlock and < TransmitterBlock + 4 * TransmitterStride
            && inSlot is 1 or 2)
            return ByKey[TransmitterBlock + inSlot].Validate(value);

        throw new ArgumentException($"0x{key:x} is not a setting confirmed as safe to write");
    }

    private const int TransmitterBlock = 0x400;
    private const int TransmitterStride = 0x20;

    private static SettingKey R(int key, string name, string category,
        int? min = null, int? max = null, bool writable = true, string note = "") =>
        new(key, name, category, SettingKind.Range, min, max, null, writable, note);

    private static SettingKey T(int key, string name, string category,
        bool writable = true, string note = "") =>
        new(key, name, category, SettingKind.Toggle, null, null, null, writable, note);

    private static SettingKey Text(int key, string name, string category,
        bool writable = true, string note = "") =>
        new(key, name, category, SettingKind.Text, null, null, null, writable, note);

    private static SettingKey E(int key, string name, string category,
        IReadOnlyDictionary<int, string> options, string note = "") =>
        new(key, name, category, SettingKind.Enum, null, null, options, true, note);

    private static List<SettingKey> Build()
    {
        var keys = new List<SettingKey>
        {
            R(0x240, "battery", "GSI", 0, 100, writable: false, note: "reported percentage"),
            E(0x280, "auto_shutoff", "GSI", AutoShutoffOptions,
              "confirmed against Swarm: value 4 shows as 30 minutes"),
            R(0x2A0, "master_volume", "GSI", 0, 100,
              note: "a MIRROR of the Windows output volume, not a control. Writing it "
                  + "changes only the number the headset reports, and Windows overwrites "
                  + "that the moment anything touches the volume. To change what you "
                  + "hear, set the Windows volume instead and this follows."),
            R(0x320, "link_rssi", "BT", writable: false,
              note: "signal strength. Sits in the BT block but is NOT the phone link: it "
                  + "keeps reporting a steady value with the phone's Bluetooth off, so it "
                  + "most likely measures the 2.4GHz link to the transmitter. Reported "
                  + "signed in updates, unsigned in reads."),

            // 0x4xx is the four transmitter slots, one per 0x20. These two sit
            // inside the first slot's block and come back in its "control"
            // array, so they are readable; a parser that drops that reply makes
            // them look write-only.
            R(0x401, "led_brightness_1", "TX1", 0, 100,
              note: "light 1: on the Charging Dock, the status ring, which turns purple "
                  + "when the link is running high bandwidth. Slot one's; reads back as "
                  + "control[1] of that slot"),
            R(0x402, "led_brightness_2", "TX1", 0, 100,
              note: "light 2: on the Charging Dock, the battery slot ring. Slot one's"),

            R(0x510, "game_chat_mix", "3DT", 0, 100, writable: false,
              note: "the chat wheel reports its position here. The headset cannot mix on "
                  + "a PC — it receives one stream — so treat this as an INPUT and apply "
                  + "the mix on the PC, which is what Swarm II does. Writing it is ignored: "
                  + "it reads back unchanged and the wheel carries on from where it was. "
                  + "Measured"),

            T(0x600, "mic_muted", "Mic", writable: false,
              note: "1 while the boom arm is flipped up, which mutes the mic. Writing "
                  + "it is accepted and does not mute: the mic went on picking up "
                  + "speech. Measured"),
            R(0x610, "mic_volume", "Mic", 0, 100,
              note: "a MIRROR of the Windows capture level for this microphone, not a "
                  + "control — the same arrangement as master volume. Set the Windows "
                  + "capture level instead. Measured, not assumed."),
            R(0x620, "mic_monitoring", "Mic", 0, 100),
            T(0x630, "ai_noise_reduction", "Mic"),

            T(0x700, "noise_gate", "SAF"),
            R(0x710, "noise_gate_threshold", "SAF", 0, 100),
            T(0x720, "superhuman_hearing", "SAF"),
            T(0x750, "anc", "SAF"),
            R(0x760, "anc_level", "SAF", 0, 100),

            E(0xA20, "dial_function", "Enc", DialOptions),
            E(0xB20, "mode_button_function", "Btn", ModeButtonOptions),

            // 0x730 and 0x740 are named from Swarm II's Superhuman Hearing
            // controls and not matched by capture, but confirmed by ear: each
            // type and intensity audibly changes the sound. The values after
            // them are read back but not matched to a control, so they are
            // named with the best current hypothesis and left non-writable.
            E(0x730, "shh_preset", "SAF", ShhPresetOptions),
            R(0x740, "shh_level", "SAF", 0, 100, note: "Superhuman Hearing intensity"),
            R(0x640, "mic_unknown_640", "Mic", writable: false,
              note: "UNCONFIRMED. Candidates: boom-mic-present flag, or an AI noise "
                  + "reduction mode sitting after the enable at 0x630"),
            R(0x650, "mic_unknown_650", "Mic", writable: false,
              note: "UNCONFIRMED. Candidates: high-bandwidth mic toggle, or a sidetone "
                  + "mute flag"),
            R(0xA00, "enc_unknown_a00", "Enc", writable: false,
              note: "UNCONFIRMED, weak. Possibly a changed-from-default flag"),
            R(0xA10, "enc_unknown_a10", "Enc", writable: false,
              note: "UNCONFIRMED. Possibly a mirror of the dial assignment at 0xa20"),
            R(0xA30, "enc_unknown_a30", "Enc", writable: false,
              note: "UNCONFIRMED. Possibly the live value of whatever the dial is "
                  + "currently assigned to"),
            R(0xB00, "btn_unknown_b00", "Btn", writable: false,
              note: "UNCONFIRMED, weak. Btn-block counterpart of 0xa00"),
            R(0xB10, "btn_unknown_b10", "Btn", writable: false,
              note: "UNCONFIRMED, weak. Possibly a mirror of 0xb20, or the function "
                  + "bound to press-and-hold on the Mode button"),
            R(0x1200, "aqg_unknown_1200", "AQG", writable: false,
              note: "UNCONFIRMED. Not the EQ bank selector — the mic equaliser turned "
                  + "out to be its own category at 0x13xx"),

            R(0x1210, "eq_preset", "AQG", 0, 255,
              note: "preset id, NOT a sequential index: 4 = Vocal Boost is a factory "
                  + "preset, and custom slots start at 16. Writing an id, then the ten "
                  + "bands, then the name is how a preset is saved — there is no save "
                  + "command."),
            Text(0x12C0, "eq_preset_name", "AQG"),

            R(0x1310, "mic_eq_preset", "AQM", 0, 255,
              note: "preset id, NOT a sequential index. 1 = Signature Sound, 3 = Clarity, "
                  + "4 = Smooth; custom slots start at 16."),
            Text(0x13C0, "mic_eq_preset_name", "AQM"),

            // Bluetooth: readable, with the verb RBT rather than SBT.
            R(0x300, "bt_unknown_300", "BT", writable: false,
              note: "NOT a Bluetooth switch. Accepts a write and persists it across a "
                  + "full power cycle, but Bluetooth still connects either way."),
            R(0x310, "bt_unknown_310", "BT", writable: false),
            R(0x330, "bt_unknown_330", "BT", writable: false),
            R(0x340, "bt_unknown_340", "BT", writable: false),
            R(0x350, "bt_unknown_350", "BT", writable: false),

            // CEC: three matching pairs of count and delete register. Writing
            // a preset's name to the register removes it; there is no delete
            // by slot id. Reading it back gives the last name deleted, so in a
            // capture it looks like a name mirror.
            R(0x1600, "game_preset_count", "CEC", writable: false,
              note: "how many custom game EQ presets are stored"),
            Text(0x1610, "delete_game_preset", "CEC",
              note: "write a custom game preset's NAME to delete it; reads back as the "
                  + "last name deleted. Confirmed: the count at 0x1600 drops and the "
                  + "slot stops answering."),
            R(0x1620, "mic_preset_count", "CEC", writable: false),
            Text(0x1630, "delete_mic_preset", "CEC",
              note: "the microphone bank's counterpart to 0x1610. Same shape, not yet "
                  + "exercised — there were no custom mic presets to delete."),
            R(0x1640, "cec_unknown_1640", "CEC", writable: false,
              note: "UNCONFIRMED. Third count/delete pair with 0x1650. Reads 1, so some "
                  + "third bank has one custom preset in it — the Bluetooth equaliser is "
                  + "the likely candidate."),
            Text(0x1650, "cec_unknown_1650", "CEC", writable: false,
              note: "UNCONFIRMED. Presumed delete register for whatever 0x1640 counts."),

            // The slot categories answer as nested objects rather than
            // strings, so they are listed only so a raw view can name them.
            Text(0x400, "transmitter_1", "TX1", writable: false,
              note: "transmitter slot 1: paired hardware, firmware, MAC, and the two "
                  + "lighting brightnesses"),
            Text(0x420, "transmitter_2", "TX2", writable: false),
            Text(0x440, "transmitter_3", "TX3", writable: false),
            Text(0x460, "transmitter_4", "TX4", writable: false),

            // Identity, from SInf.
            Text(0x100, "usb_vendor_id", "Inf", writable: false),
            Text(0x110, "usb_product_id", "Inf", writable: false,
              note: "reads 229E — the headset's own id, not the transmitter's"),
            Text(0x120, "serial_number", "Inf", writable: false),
            Text(0x130, "firmware_version", "Inf", writable: false),
            R(0x140, "inf_unknown_140", "Inf", writable: false),
            // Watched while the headset was moved between transmitters; see
            // LinkState for what it is and what it is not.
            R(0x150, "transmitter_flag", "Inf", writable: false,
              note: "changes when the headset moves between transmitters. What the "
                  + "value means is not known, possibly the slot in use; rely only on "
                  + "the change. Not a sign that audio is flowing"),
            R(0x160, "inf_unknown_160", "Inf", writable: false),

            R(0x230, "sound_link", "GSI", writable: false,
              note: "2 while a transmitter is sending the headset sound, 0 while none "
                  + "is. Wireless only: it says nothing about the USB-C cable"),

            // Confirmed by driving the Swarm II MOBILE app and watching which
            // value moved. Neither of these exists in the desktop app.
            R(0x2D0, "voice_prompt_volume", "GSI", 0, 100,
              note: "the desktop Swarm II has no control for this — mobile only"),
            T(0x2E0, "wake_on_motion", "GSI",
              note: "the desktop Swarm II has no control for this — mobile only"),
            // Identified by watching every value while the hardware was
            // operated, then confirmed in reverse. See LinkState for the
            // transitions and what each bit means.
            R(0x290, "link_state", "GSI", writable: false,
              note: "bit 0 is Bluetooth connected; bits 1-2 are how it is "
                  + "attached, 1 = 2.4GHz and 2 = USB"),
            R(0x250, "charging", "GSI", writable: false,
              note: "1 while the battery is filling. Not merely 'a cable is "
                  + "attached' — 0x290 already says that, and a second flag "
                  + "that only repeated it would be redundant"),
        };

        // Ten contiguous bands per bank at 0x10 stride, matching Turtle
        // Beach's documented ten-band equaliser; the last two are bands too,
        // not other settings. Values are tenths of a decibel.
        for (int band = 1; band <= 10; band++)
        {
            keys.Add(R(0x1220 + 0x10 * (band - 1), $"eq_band_{band}", "AQG", -90, 90));
            keys.Add(R(0x1320 + 0x10 * (band - 1), $"mic_eq_band_{band}", "AQM", -90, 90));
        }

        // The ten custom equaliser slots, five per bank. Like the transmitter
        // slots they answer as nested objects, and are listed only so a raw
        // view can name them.
        for (int slot = 1; slot <= 5; slot++)
        {
            keys.Add(Text(0x1700 + 0x20 * (slot - 1), $"game_preset_slot_{slot}",
                $"CG{slot}", writable: false,
                note: slot == 1 ? "custom game equaliser slot: name and all ten bands" : ""));
            keys.Add(Text(0x1800 + 0x20 * (slot - 1), $"mic_preset_slot_{slot}",
                $"CM{slot}", writable: false,
                note: slot == 1 ? "custom microphone equaliser slot" : ""));
        }

        return keys;
    }
}
