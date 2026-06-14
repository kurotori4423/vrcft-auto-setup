# VRCFT Auto Setup

English | [日本語](README.ja.md)

Unity Editor extension that generates a [VRChat Face Tracking (VRCFT)](https://docs.vrcft.io/) animation setup from blend shapes on a target avatar.

It detects facial expression blend shapes on the avatar and creates an Animator Controller, Expression Parameters, Expression Menu, and [Modular Avatar](https://modular-avatar.nadena.dev/) installer object for VRCFT v2 parameters.

## Features

- Generates VRCFT Animator layers, synced parameters, and Expression Menu assets with one click.
- Uses [Modular Avatar](https://modular-avatar.nadena.dev/) for a non-destructive setup that does not directly rewrite the original avatar hierarchy.
- Detects shape keys with common prefixes, suffixes, and naming differences, making it usable on avatars that are hard to support with fixed templates alone.
- Supports Binary Sync to reduce synced parameter cost. The Standard preset is designed around roughly 78 bits including control parameters.
- Adds separate local and remote smoothing controls to reduce stepped motion from Binary Sync and lower remote update rates.
- Chooses between detailed VRCFT parameters and Simplified Tracking parameters based on the blend shapes available on the avatar.
- Supports Japanese and English in the editor UI, with selectable language for generated Expression Menu labels.

## Installation

Add the VPM repository to VRChat Creator Companion, then add `VRCFT Auto Setup` to your project.

[Add repository to VCC](https://kurotori4423.github.io/vpm.kurotori4423/add-repo.html)

For manual setup, use this repository URL:

```text
https://kurotori4423.github.io/vpm.kurotori4423/vpm.json
```

## Dependencies

- VRChat SDK - Avatars
- [Modular Avatar](https://modular-avatar.nadena.dev/)

## Usage

1. Open `Tools/Kurotori/VRCFT Auto Setup` from the Unity Editor menu.
2. Assign an avatar with a `VRCAvatarDescriptor` to `Target Avatar`.
3. Click `Detect` to inspect the face mesh and detected blend shape mappings.
4. Adjust the preset, generation settings, enabled parameters, and bit counts as needed.
5. Click `Generate and Install` to save generated assets and place a Modular Avatar installer object under the avatar root.

Generated assets are saved to `Assets/VrcftAutoSetup/Generated/<Avatar Name>/` by default.

## Main Features

### Automatic Blend Shape Detection

The detector scans `SkinnedMeshRenderer` components under the target avatar and matches blend shapes to VRCFT v2 parameters.

It expects common names from ARKit such as `jawOpen`, Unified Expressions names such as `JawOpen`, and SRanipal-style names such as `Jaw_Open`. Matching normalizes case and some symbol differences.

Parameters that are not detected automatically can be assigned manually from the `Manual` column in the parameter list.

### Animator Controller Generation

The tool generates an FX Animator Controller from the detection result.

- Layers that receive VRCFT v2 parameters
- Optional Binary decoding layers
- Optional smoothing layers
- Blend shape driving layers
- Control layers for `EyeTrackingActive` and `LipTrackingActive`

When EyeLook is enabled, it also generates an Additive Animator Controller that drives Humanoid eye muscles.

### Modular Avatar Installation

The generated Animator Controller, synced parameters, and Expression Menu are bundled into a Modular Avatar installer object and placed directly under the avatar root.

If an installer object with the same name already exists, it is replaced during generation.

### Expression Menu Generation

When `Generate Menu` is enabled, the tool adds a `Face Tracking` submenu through Modular Avatar Menu Installer.

Generated menu items depend on the selected settings.

- `Eye Tracking`: toggles VRCFT control for eyes and gaze.
- `Lip Tracking`: toggles VRCFT control for mouth and tongue.
- `Voice LipSync Blend`: switches whether VRChat's standard Viseme LipSync is preferred while speaking.
- `Smoothing`: controls local smoothing amount with a Radial Puppet.

## Generation Settings

### Preset

Selects the range of VRCFT parameters to generate.

| Preset | Scope | Main Use |
| --- | --- | --- |
| `Minimal` | Lightweight set focused on major eyes, eyelids, jaw opening, smile/mouth corners, and tongue. | Useful when you want to minimize synced bits. |
| `Standard` | Standard set with independent eyes and major mouth, cheek, and brow parameters. | Recommended starting point. |
| `Full` | Includes finer brows, cheeks, nose, tongue, and additional mouth expressions. | Best for avatars with many supported blend shapes. |

Changing the preset removes parameters outside the selected scope from the list.

### Parameter Mode

Selects how detailed parameters and Simplified Tracking parameters are prioritized.

| Mode | Preferred Parameters | Main Use |
| --- | --- | --- |
| `Hybrid` | Uses detailed parameters when the required left/right or positive/negative coverage is complete, and falls back to Simplified parameters when details are missing. | Default mode. It uses detailed input where possible and simplified input where needed. |
| `Simplified` | Simplified Tracking parameters. | Useful when you want fewer synced parameters or prefer combined expression shapes. If only left/right detailed shape keys exist, they can still be combined into one simplified parameter animation. |
| `Detailed` | Detailed VRCFT parameters. | Useful when you explicitly want finer tracking input. |

For example, when both `CheekPuffSuckLeft` and `CheekPuffSuckRight` are available, `Hybrid` uses detailed left/right parameters. If only one side is available, it falls back to `CheekPuffSuck` to avoid partially detailed driving.

### Binary Sync

Binary Sync stores VRCFT Float parameters as multiple Bool bits and decodes them back into Float values inside the Animator.

When enabled, it can reduce synced parameter cost. When disabled, each enabled parameter is treated as a Float and estimated as 8 bits.

### Override Bits

Used when Binary Sync is enabled.

`0` uses the default bit count for each parameter. Values `1` or higher apply the same bit count to all parameters.

Higher values improve precision but increase synced bits. Lower values are cheaper but make expression motion more stepped.

### Write Defaults

Controls the Write Defaults policy for generated Animator States.

| Setting | Behavior | Note |
| --- | --- | --- |
| `On` | Generates all states with Write Defaults On. | Default. |
| `Mix` | Uses Off by default, but keeps On where required by AAP or Direct BlendTree behavior. | Useful when matching an existing avatar Animator setup. |
| `Off` | Generates all states with Write Defaults Off. | Smoothing is disabled in this mode. |

Adjust this to match the existing Animator style of your avatar.

### Smoothing

Smooths VRCFT parameter changes inside the Animator.

| Item | Target | How to Adjust |
| --- | --- | --- |
| `Local` | Smoothing amount visible in your own local environment. | Adjustable from the generated Expression Menu `Smoothing` control. |
| `Remote` | Smoothing amount for how other users see your expressions. | Set in the generation settings. |

Lower values follow input more quickly. Higher values smooth changes more heavily. This is useful for reducing stepped motion from Binary Sync or low remote update rates. Smoothing is disabled when Write Defaults is `Off`.

### EyeLook

Generates an Additive Animator Controller that drives Humanoid eye muscles from VRCFT gaze parameters.

The current implementation supports `HumanoidMuscleFixed`. `BlendShapes` exists as a UI option, but this mode does not generate the Additive EyeLook Controller yet.

### Prefer Viseme LipSync While Speaking

When enabled, VRChat's standard Viseme LipSync is preferred while the `Voice` parameter is above the threshold.

Use this when you want VRCFT mouth tracking while silent, but standard lip sync during speech.

### Voice Threshold

The `Voice` volume threshold used by `Prefer Viseme LipSync While Speaking`.

Lower values switch to standard lip sync with quieter speech. Higher values switch only for louder speech.

### Generate Menu

When enabled, the tool generates a `Face Tracking` submenu and appends it through [Modular Avatar](https://modular-avatar.nadena.dev/) Menu Installer.

Disable this if you want to manually place the generated controls in your own menu.

### Output Folder

Destination folder for generated assets.

The default is `Assets/VrcftAutoSetup/Generated`. The actual output path adds a folder named after the avatar below this folder.

## Parameter List

| Column | Description |
| --- | --- |
| `Manual` | Opens manual blend shape override fields. |
| `Enabled` | Toggles whether the VRCFT parameter is included in generation. |
| `Parameter` | VRCFT v2 parameter name to generate. |
| `Bits` | Bit count used by Binary Sync. |
| `Detected Shapes` | Blend shapes assigned by automatic detection or manual override. |

Undetected parameters are shown in gray. If the required blend shape exists, enter its name from the manual fields.

## Generated Files

With default settings, the output folder contains assets such as:

- `FX_FaceTracking.controller`
- `Animations/`
- `Animations/Binary/`
- `Animations/Smooth/`
- `Additive/Additive_EyeTracking.controller`
- `Additive/Animations/`
- `Additive/Masks/VRCFT_HeadOnly.mask`
- `Menu/FT_Root.asset`
- `Menu/FT_Menu.asset`
- `<Avatar Name>_VRCFT.prefab`

Some folders or assets may not be generated depending on settings and detection results.

## References

This tool is based on ideas from the following projects and templates:

- [regzo2/OSCmooth](https://github.com/regzo2/OSCmooth)
- [ADJERRY91/VRCFACETRACKING-TEMPLATES](https://github.com/ADJERRY91/VRCFACETRACKING-TEMPLATES)

## License

MIT License. See [LICENSE](LICENSE) for details.
