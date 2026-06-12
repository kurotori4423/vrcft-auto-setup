using System;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// EyeLook の駆動方式。
    /// </summary>
    public enum EyeLookMode
    {
        /// <summary>Descriptor の RotationStates から Humanoid の目 muscle を駆動。</summary>
        HumanoidFromDescriptor = 0,
        /// <summary>ブレンドシェイプで駆動。</summary>
        BlendShapes = 1,
    }

    /// <summary>
    /// 生成オプション。
    /// </summary>
    [Serializable]
    public class VrcftAutoSetupSettings
    {
        /// <summary>使用するパラメーターセット。</summary>
        public VrcftPreset preset = VrcftPreset.Standard;

        /// <summary>Binary同期 (ビット削減) を使うか。</summary>
        public bool useBinary = true;

        /// <summary>0以上なら全パラメーターのビット数を一律上書き (0=各エントリ既定を使用)。</summary>
        public int defaultBinaryBitsOverride = 0;

        /// <summary>OSCmooth方式のスムージングを有効にするか。</summary>
        public bool enableSmoothing = true;

        /// <summary>ローカルスムージング (ブレンド比, 0..1)。</summary>
        public float localSmoothness = 0.7f;

        /// <summary>リモートスムージング (ブレンド比, 0..1)。</summary>
        public float remoteSmoothness = 0.9f;

        /// <summary>EyeLook (視線) アニメを生成するか。</summary>
        public bool enableEyeLook = true;

        /// <summary>EyeLook 駆動方式。</summary>
        public EyeLookMode eyeLookMode = EyeLookMode.HumanoidFromDescriptor;

        /// <summary>Expression メニューを生成するか。</summary>
        public bool addMenu = true;

        /// <summary>生成ステートの WriteDefaults。</summary>
        public bool writeDefaults = true;

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
