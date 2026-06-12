using System.Collections.Generic;
using UnityEditor.Animations;

namespace Kurotori.VrcftAutoSetup.Editor
{
    /// <summary>
    /// 同期パラメーターの種別。
    /// </summary>
    public enum VrcftParameterKind
    {
        Bool,
        Float,
    }

    /// <summary>
    /// 生成された同期パラメーター1件。Phase C の MA Parameters 構築に使う。
    /// </summary>
    public sealed class VrcftSyncedParameter
    {
        public string name;
        public VrcftParameterKind kind;
        public float defaultValue;
        public bool saved;

        /// <summary>local-only パラメーター (例 OSCm/Local/FloatSmoothing)。Phase C で localOnly フラグとして処理。</summary>
        public bool localOnly;

        public VrcftSyncedParameter() { }

        public VrcftSyncedParameter(string name, VrcftParameterKind kind, float defaultValue, bool saved, bool localOnly = false)
        {
            this.name = name;
            this.kind = kind;
            this.defaultValue = defaultValue;
            this.saved = saved;
            this.localOnly = localOnly;
        }
    }

    /// <summary>
    /// Phase B 生成結果。
    /// </summary>
    public sealed class VrcftGenerationResult
    {
        /// <summary>生成された FX AnimatorController。</summary>
        public AnimatorController fxController;

        /// <summary>同期パラメーター一覧 (Phase C の MA Parameters 構築用)。</summary>
        public List<VrcftSyncedParameter> syncedParameters = new List<VrcftSyncedParameter>();

        /// <summary>生成先ディレクトリ ("Assets/VrcftAutoSetup/Generated/&lt;アバター名&gt;")。</summary>
        public string outputDir;

        /// <summary>生成した全クリップパス (ログ用)。</summary>
        public List<string> generatedClipPaths = new List<string>();
    }
}
