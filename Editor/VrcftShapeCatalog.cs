using System;
using System.Collections.Generic;
using System.Linq;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// パラメーターが含まれるプリセット段階。Minimal ⊂ Standard ⊂ Full。
    /// </summary>
    public enum VrcftPreset
    {
        Minimal = 0,
        Standard = 1,
        Full = 2,
    }

    /// <summary>
    /// 1つの「駆動スロット」。複数のブレンドシェイプを同時駆動できるFTパラメーターでは
    /// スロットが複数になる (例: MouthUpperUp は Left / Right の2スロット)。
    /// 各スロットは命名規格違いのエイリアス候補リストを持つ。
    /// </summary>
    [Serializable]
    public sealed class ShapeSlot
    {
        /// <summary>エイリアス候補 (ARKit camelCase / Unified Expressions PascalCase / SRanipal Snake_Case)。</summary>
        public readonly string[] Aliases;

        public ShapeSlot(params string[] aliases)
        {
            Aliases = aliases ?? Array.Empty<string>();
        }
    }

    /// <summary>
    /// FTパラメーター1件のカタログ定義。
    /// </summary>
    public sealed class VrcftCatalogEntry
    {
        /// <summary>短縮名 (例 "JawOpen")。FullName で "FT/v2/JawOpen" を返す。</summary>
        public string ParameterName { get; }

        /// <summary>FT/v2/ 接頭辞付きの完全パラメーター名。</summary>
        public string FullName => VrcftShapeCatalog.ParameterPrefix + ParameterName;

        /// <summary>-1..1 双方向か (true) / 0..1 単方向か (false)。</summary>
        public bool TwoSided { get; }

        /// <summary>正側の駆動スロット群 (各スロットがエイリアス候補を持つ)。</summary>
        public ShapeSlot[] PositiveShapes { get; }

        /// <summary>負側の駆動スロット群 (双方向のみ。なければ空)。</summary>
        public ShapeSlot[] NegativeShapes { get; }

        /// <summary>Binary時の推奨ビット数。</summary>
        public int DefaultBinaryBits { get; }

        /// <summary>どのプリセットから含まれるか。</summary>
        public VrcftPreset Preset { get; }

        /// <summary>0..1 の基準デフォルト値 (EyeLid 等は 0.75)。</summary>
        public float DefaultValue { get; }

        /// <summary>EyeLid のような専用構造であることを示す (Blink/Wide 別フィールド)。</summary>
        public bool IsEyeLid { get; }

        /// <summary>EyeLid専用: 閉眼(blink)スロット。</summary>
        public ShapeSlot EyeLidBlink { get; }

        /// <summary>EyeLid専用: 見開き(wide)スロット。</summary>
        public ShapeSlot EyeLidWide { get; }

        public VrcftCatalogEntry(
            string parameterName,
            bool twoSided,
            ShapeSlot[] positiveShapes,
            ShapeSlot[] negativeShapes,
            int defaultBinaryBits,
            VrcftPreset preset,
            float defaultValue = 0f,
            bool isEyeLid = false,
            ShapeSlot eyeLidBlink = null,
            ShapeSlot eyeLidWide = null)
        {
            ParameterName = parameterName;
            TwoSided = twoSided;
            PositiveShapes = positiveShapes ?? Array.Empty<ShapeSlot>();
            NegativeShapes = negativeShapes ?? Array.Empty<ShapeSlot>();
            DefaultBinaryBits = defaultBinaryBits;
            Preset = preset;
            DefaultValue = defaultValue;
            IsEyeLid = isEyeLid;
            EyeLidBlink = eyeLidBlink;
            EyeLidWide = eyeLidWide;
        }

        /// <summary>このエントリが対象とする全スロット (正側+負側+EyeLid専用) を列挙。</summary>
        public IEnumerable<ShapeSlot> AllSlots()
        {
            if (IsEyeLid)
            {
                if (EyeLidBlink != null) yield return EyeLidBlink;
                if (EyeLidWide != null) yield return EyeLidWide;
                yield break;
            }
            foreach (var s in PositiveShapes) yield return s;
            foreach (var s in NegativeShapes) yield return s;
        }
    }

    /// <summary>
    /// VRCFT v2 パラメーターの静的カタログ。
    /// エイリアスはARKit 52規格(camelCase)を網羅し、知る範囲でUnified Expressions / SRanipal名を追加。
    /// </summary>
    public static class VrcftShapeCatalog
    {
        /// <summary>FTパラメーター完全名の接頭辞。</summary>
        public const string ParameterPrefix = "FT/v2/";

        private static List<VrcftCatalogEntry> _entries;

        /// <summary>全カタログエントリ (読み取り専用)。</summary>
        public static IReadOnlyList<VrcftCatalogEntry> Entries
        {
            get
            {
                if (_entries == null) _entries = Build();
                return _entries;
            }
        }

        /// <summary>指定プリセット以下に含まれるエントリを返す。</summary>
        public static IEnumerable<VrcftCatalogEntry> EntriesForPreset(VrcftPreset preset)
        {
            return Entries.Where(e => (int)e.Preset <= (int)preset);
        }

        // ------ ヘルパー ------
        private static ShapeSlot S(params string[] aliases) => new ShapeSlot(aliases);
        private static ShapeSlot[] Slots(params ShapeSlot[] slots) => slots;
        private static readonly ShapeSlot[] None = Array.Empty<ShapeSlot>();

        private static List<VrcftCatalogEntry> Build()
        {
            var list = new List<VrcftCatalogEntry>();

            // =========================== Eye系 ===========================
            // EyeLeftX / EyeRightX : ボーン駆動が基本だがシェイプ駆動エイリアスも定義。
            // 正側=外向き(Out)、負側=内向き(In)とする。
            list.Add(new VrcftCatalogEntry("EyeLeftX", true,
                Slots(S("eyeLookOutLeft", "EyeLeftX", "Eye_Left_Right", "EYES_LOOK_OUT_L")),
                Slots(S("eyeLookInLeft", "EYES_LOOK_IN_L")),
                4, VrcftPreset.Minimal));
            list.Add(new VrcftCatalogEntry("EyeRightX", true,
                Slots(S("eyeLookOutRight", "EyeRightX", "Eye_Right_Right", "EYES_LOOK_OUT_R")),
                Slots(S("eyeLookInRight", "EYES_LOOK_IN_R")),
                4, VrcftPreset.Minimal));
            // EyeY : 上下。正側=上(Up)、負側=下(Down)。
            list.Add(new VrcftCatalogEntry("EyeY", true,
                Slots(S("eyeLookUpLeft", "eyeLookUpRight", "EyeUp", "Eye_Up", "EYES_LOOK_UP_L", "EYES_LOOK_UP_R")),
                Slots(S("eyeLookDownLeft", "eyeLookDownRight", "EyeDown", "Eye_Down", "EYES_LOOK_DOWN_L", "EYES_LOOK_DOWN_R")),
                4, VrcftPreset.Minimal));

            // EyeLid (専用構造): 0..1, default 0.75。0-0.75=閉眼(blink逆向き), 0.75-1=見開き(wide)。
            list.Add(new VrcftCatalogEntry("EyeLidLeft", false, None, None, 4, VrcftPreset.Minimal,
                defaultValue: 0.75f, isEyeLid: true,
                eyeLidBlink: S("eyeBlinkLeft", "EyeClosedLeft", "Eye_Left_Blink", "EYES_CLOSED_L"),
                eyeLidWide: S("eyeWideLeft", "EyeWideLeft", "Eye_Left_Wide", "EYES_WIDEN_L")));
            list.Add(new VrcftCatalogEntry("EyeLidRight", false, None, None, 4, VrcftPreset.Minimal,
                defaultValue: 0.75f, isEyeLid: true,
                eyeLidBlink: S("eyeBlinkRight", "EyeClosedRight", "Eye_Right_Blink", "EYES_CLOSED_R"),
                eyeLidWide: S("eyeWideRight", "EyeWideRight", "Eye_Right_Wide", "EYES_WIDEN_R")));

            // EyeSquint (Full)
            list.Add(new VrcftCatalogEntry("EyeSquintLeft", false,
                Slots(S("eyeSquintLeft", "EyeSquintLeft", "Eye_Left_Squint", "EYES_SQUINT_L")), None,
                3, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("EyeSquintRight", false,
                Slots(S("eyeSquintRight", "EyeSquintRight", "Eye_Right_Squint", "EYES_SQUINT_R")), None,
                3, VrcftPreset.Full));

            // =========================== Brow系 ===========================
            // BrowExpression : 正=上げ(browUp系), 負=下げ(browDown系)。
            // ARKit の browInnerUp は両側一体のため左右どちらにも候補として含める。
            list.Add(new VrcftCatalogEntry("BrowExpressionLeft", true,
                Slots(S("browOuterUpLeft", "browInnerUp", "BrowUpLeft", "Brow_Left_Up", "OUTER_BROW_RAISER_L")),
                Slots(S("browDownLeft", "BrowDownLeft", "Brow_Left_Down", "BROW_LOWERER_L")),
                3, VrcftPreset.Standard));
            list.Add(new VrcftCatalogEntry("BrowExpressionRight", true,
                Slots(S("browOuterUpRight", "browInnerUp", "BrowUpRight", "Brow_Right_Up", "OUTER_BROW_RAISER_R")),
                Slots(S("browDownRight", "BrowDownRight", "Brow_Right_Down", "BROW_LOWERER_R")),
                3, VrcftPreset.Standard));

            // Full時の個別 (ARKitにはbrowInnerUp/browDownL/R/browOuterUpL/Rのみなのでエイリアス薄め)
            list.Add(new VrcftCatalogEntry("BrowInnerUpLeft", false,
                Slots(S("browInnerUp", "BrowInnerUpLeft", "INNER_BROW_RAISER_L")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("BrowInnerUpRight", false,
                Slots(S("browInnerUp", "BrowInnerUpRight", "INNER_BROW_RAISER_R")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("BrowOuterUpLeft", false,
                Slots(S("browOuterUpLeft", "BrowOuterUpLeft", "OUTER_BROW_RAISER_L")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("BrowOuterUpRight", false,
                Slots(S("browOuterUpRight", "BrowOuterUpRight", "OUTER_BROW_RAISER_R")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("BrowLowererLeft", false,
                Slots(S("browDownLeft", "BrowLowererLeft", "BROW_LOWERER_L")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("BrowLowererRight", false,
                Slots(S("browDownRight", "BrowLowererRight", "BROW_LOWERER_R")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("BrowPinchLeft", false,
                Slots(S("BrowPinchLeft")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("BrowPinchRight", false,
                Slots(S("BrowPinchRight")), None, 2, VrcftPreset.Full));

            // =========================== Jaw/Mouth系 ===========================
            list.Add(new VrcftCatalogEntry("JawOpen", false,
                Slots(S("jawOpen", "JawOpen", "Jaw_Open", "JAW_DROP")), None,
                4, VrcftPreset.Minimal));
            // JawX : 正=右(jawRight), 負=左(jawLeft)
            list.Add(new VrcftCatalogEntry("JawX", true,
                Slots(S("jawRight", "JawRight", "Jaw_Right", "JAW_SIDEWAYS_RIGHT")),
                Slots(S("jawLeft", "JawLeft", "Jaw_Left", "JAW_SIDEWAYS_LEFT")),
                3, VrcftPreset.Standard));
            list.Add(new VrcftCatalogEntry("JawForward", false,
                Slots(S("jawForward", "JawForward", "Jaw_Forward", "JAW_THRUST")), None,
                2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("MouthClosed", false,
                Slots(S("mouthClose", "MouthClosed", "Mouth_Close", "LIPS_TOWARD")), None,
                3, VrcftPreset.Minimal));
            // MouthX : 口全体の横移動。正=右(mouthRight), 負=左(mouthLeft)
            list.Add(new VrcftCatalogEntry("MouthX", true,
                Slots(S("mouthRight", "MouthRight", "Mouth_Right")),
                Slots(S("mouthLeft", "MouthLeft", "Mouth_Left")),
                3, VrcftPreset.Standard));
            // SmileFrown : 正=smile, 負=frown
            list.Add(new VrcftCatalogEntry("SmileFrownLeft", true,
                Slots(S("mouthSmileLeft", "MouthSmileLeft", "Mouth_Smile_Left", "LIP_CORNER_PULLER_L")),
                Slots(S("mouthFrownLeft", "MouthFrownLeft", "Mouth_Frown_Left", "LIP_CORNER_DEPRESSOR_L")),
                4, VrcftPreset.Minimal));
            list.Add(new VrcftCatalogEntry("SmileFrownRight", true,
                Slots(S("mouthSmileRight", "MouthSmileRight", "Mouth_Smile_Right", "LIP_CORNER_PULLER_R")),
                Slots(S("mouthFrownRight", "MouthFrownRight", "Mouth_Frown_Right", "LIP_CORNER_DEPRESSOR_R")),
                4, VrcftPreset.Minimal));
            // MouthUpperUp : 左右2スロット
            list.Add(new VrcftCatalogEntry("MouthUpperUp", false,
                Slots(S("mouthUpperUpLeft", "MouthUpperUpLeft", "Mouth_Upper_Up_Left", "UPPER_LIP_RAISER_L"), S("mouthUpperUpRight", "MouthUpperUpRight", "Mouth_Upper_Up_Right", "UPPER_LIP_RAISER_R")), None,
                3, VrcftPreset.Standard));
            // MouthLowerDown : 左右2スロット
            list.Add(new VrcftCatalogEntry("MouthLowerDown", false,
                Slots(S("mouthLowerDownLeft", "MouthLowerDownLeft", "Mouth_Lower_Down_Left", "LOWER_LIP_DEPRESSER_L"), S("mouthLowerDownRight", "MouthLowerDownRight", "Mouth_Lower_Down_Right", "LOWER_LIP_DEPRESSOR_R")), None,
                3, VrcftPreset.Standard));
            list.Add(new VrcftCatalogEntry("LipPucker", false,
                Slots(S("mouthPucker", "LipPucker", "Mouth_Pout")), None,
                3, VrcftPreset.Standard));
            list.Add(new VrcftCatalogEntry("LipFunnel", false,
                Slots(S("mouthFunnel", "LipFunnel")), None,
                3, VrcftPreset.Standard));
            // MouthStretch (Full)
            list.Add(new VrcftCatalogEntry("MouthStretchLeft", false,
                Slots(S("mouthStretchLeft", "MouthStretchLeft", "LIP_STRETCHER_L")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("MouthStretchRight", false,
                Slots(S("mouthStretchRight", "MouthStretchRight", "LIP_STRETCHER_R")), None, 2, VrcftPreset.Full));
            // MouthPress : 左右2スロット (Full)
            list.Add(new VrcftCatalogEntry("MouthPress", false,
                Slots(S("mouthPressLeft", "MouthPressLeft", "LIP_PRESSOR_L"), S("mouthPressRight", "MouthPressRight", "LIP_PRESSOR_R")), None,
                2, VrcftPreset.Full));
            // MouthDimple : 左右2スロット (Full)
            list.Add(new VrcftCatalogEntry("MouthDimple", false,
                Slots(S("mouthDimpleLeft", "MouthDimpleLeft", "DIMPLER_L"), S("mouthDimpleRight", "MouthDimpleRight", "DIMPLER_R")), None,
                2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("MouthRaiserUpper", false,
                Slots(S("mouthShrugUpper", "MouthRaiserUpper", "CHIN_RAISER_T")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("MouthRaiserLower", false,
                Slots(S("mouthShrugLower", "MouthRaiserLower", "Mouth_Lower_Overlay", "CHIN_RAISER_B")), None, 2, VrcftPreset.Full));

            // =========================== Cheek/Nose系 ===========================
            // CheekPuffSuck : 一体型。正=puff (ARKitはcheekPuff一体), 負=suck (無ければ空)
            list.Add(new VrcftCatalogEntry("CheekPuffSuck", true,
                Slots(S("cheekPuff", "CheekPuffLeft", "CheekPuffRight", "Cheek_Puff", "CHEEK_PUFF_L", "CHEEK_PUFF_R")),
                Slots(S("cheekSuck", "CheekSuckLeft", "CheekSuckRight", "Cheek_Suck", "CHEEK_SUCK_L", "CHEEK_SUCK_R")),
                3, VrcftPreset.Standard));
            list.Add(new VrcftCatalogEntry("CheekSquintLeft", false,
                Slots(S("cheekSquintLeft", "CheekSquintLeft", "CHEEK_RAISER_L")), None, 2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("CheekSquintRight", false,
                Slots(S("cheekSquintRight", "CheekSquintRight", "CHEEK_RAISER_R")), None, 2, VrcftPreset.Full));
            // NoseSneer : 左右2スロット (Full)
            list.Add(new VrcftCatalogEntry("NoseSneer", false,
                Slots(S("noseSneerLeft", "NoseSneerLeft", "NOSE_WRINKLER_L"), S("noseSneerRight", "NoseSneerRight", "NOSE_WRINKLER_R")), None,
                2, VrcftPreset.Full));

            // =========================== Tongue系 ===========================
            list.Add(new VrcftCatalogEntry("TongueOut", false,
                Slots(S("tongueOut", "TongueOut", "Tongue_LongStep1", "Tongue_LongStep2", "TONGUE_OUT")), None,
                3, VrcftPreset.Standard));
            // TongueX / TongueY : ARKit標準には無い (Full)
            list.Add(new VrcftCatalogEntry("TongueX", true,
                Slots(S("tongueRight", "TongueRight", "Tongue_Right")),
                Slots(S("tongueLeft", "TongueLeft", "Tongue_Left")),
                2, VrcftPreset.Full));
            list.Add(new VrcftCatalogEntry("TongueY", true,
                Slots(S("tongueUp", "TongueUp", "Tongue_Up")),
                Slots(S("tongueDown", "TongueDown", "Tongue_Down")),
                2, VrcftPreset.Full));

            return list;
        }

        /// <summary>
        /// ブレンドシェイプ名/エイリアスを正規化する。大文字小文字無視 + 記号(_、スペース)除去。
        /// </summary>
        public static string Normalize(string name)
        {
            if (string.IsNullOrEmpty(name)) return string.Empty;
            var sb = new System.Text.StringBuilder(name.Length);
            foreach (var c in name)
            {
                if (c == '_' || c == ' ' || c == '-' || c == '.') continue;
                sb.Append(char.ToLowerInvariant(c));
            }
            return sb.ToString();
        }
    }
}
