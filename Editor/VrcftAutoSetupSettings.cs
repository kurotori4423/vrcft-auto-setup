using System;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// EyeLook の駆動方式。
    /// </summary>
    public enum EyeLookMode
    {
        /// <summary>Humanoid の目 muscle を -1..1 の固定範囲で駆動。</summary>
        HumanoidMuscleFixed = 0,
        /// <summary>ブレンドシェイプで駆動。</summary>
        BlendShapes = 1,
    }

    /// <summary>
    /// 生成する Animator State の Write Defaults 方針。
    /// </summary>
    public enum VrcftWriteDefaultsMode
    {
        /// <summary>全ステートを Write Defaults On で生成する。</summary>
        On = 0,
        /// <summary>原則 Off にし、AAP/Direct BlendTree など On が必要なステートだけ On にする。</summary>
        Mix = 1,
        /// <summary>全ステートを Write Defaults Off で生成する。スムージングは使用不可。</summary>
        Off = 2,
    }

    /// <summary>
    /// 通常系とSimplified Tracking系が同じ表情を駆動できる場合の選択方針。
    /// </summary>
    public enum VrcftParameterProfile
    {
        /// <summary>検知できた通常系を優先し、足りない箇所だけ簡略系で補う。</summary>
        Hybrid = 0,
        /// <summary>簡略系を優先し、対応する簡略系がない箇所だけ通常系を使う。</summary>
        Simplified = 1,
        /// <summary>通常系を優先し、対応する通常系がない箇所だけ簡略系を使う。</summary>
        Detailed = 2,
    }

    /// <summary>
    /// 生成オプション。
    /// </summary>
    [Serializable]
    public class VrcftAutoSetupSettings
    {
        /// <summary>使用するパラメーターセット。</summary>
        public VrcftPreset preset = VrcftPreset.Standard;

        /// <summary>通常系 / Simplified Tracking 系の選択方針。</summary>
        public VrcftParameterProfile parameterProfile = VrcftParameterProfile.Hybrid;

        /// <summary>Binary同期 (ビット削減) を使うか。</summary>
        public bool useBinary = true;

        /// <summary>0以上なら全パラメーターのビット数を一律上書き (0=各エントリ既定を使用)。</summary>
        public int defaultBinaryBitsOverride = 0;

        /// <summary>OSCmooth方式のスムージングを有効にするか。</summary>
        public bool enableSmoothing = true;

        /// <summary>ローカルスムージング (ブレンド比, 0..1)。</summary>
        public float localSmoothness = 0.2f;

        /// <summary>リモートスムージング (ブレンド比, 0..1)。</summary>
        public float remoteSmoothness = 0.4f;

        /// <summary>EyeLook (視線) アニメを生成するか。</summary>
        public bool enableEyeLook = true;

        /// <summary>EyeLook 駆動方式。</summary>
        public EyeLookMode eyeLookMode = EyeLookMode.HumanoidMuscleFixed;

        /// <summary>発声中だけ VRChat 標準の Viseme リップシンクを優先するか。</summary>
        public bool enableVoiceLipSyncBlend = true;

        /// <summary>Viseme リップシンクへ切り替える Voice 音量しきい値。</summary>
        public float voiceLipSyncThreshold = 0.05f;

        /// <summary>Expression メニューを生成するか。</summary>
        public bool addMenu = true;

        /// <summary>生成ステートの Write Defaults 方針。</summary>
        public VrcftWriteDefaultsMode writeDefaultsMode = VrcftWriteDefaultsMode.On;

        /// <summary>生成アセットの出力フォルダ。</summary>
        public string outputFolder = "Assets/VrcftAutoSetup/Generated";

        /// <summary>
        /// 指定エントリの実効ビット数を返す (override 適用後)。
        /// </summary>
        public int ResolveBits(VrcftCatalogEntry entry, int uiBits)
        {
            if (defaultBinaryBitsOverride > 0) return defaultBinaryBitsOverride;
            return uiBits > 0 ? uiBits : entry.DefaultBinaryBits;
        }
    }
}
