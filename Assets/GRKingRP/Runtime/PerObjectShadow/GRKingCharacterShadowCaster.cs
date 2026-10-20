using System.Collections.Generic;
using UnityEngine;

namespace GRKingRP.PerObjectShadow
{
    /// <summary>注册角色，收集实际参与阴影绘制的 Renderer，并合并世界空间 Bounds。</summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    [AddComponentMenu("GRKingRP/Character Shadow Caster")]
    public sealed class GRKingCharacterShadowCaster : MonoBehaviour
    {
        private static readonly List<GRKingCharacterShadowCaster> s_ActiveCasters = new();
        private static int s_NextShadowCasterId = 1;
#if UNITY_EDITOR
        private static bool s_EditorMaterialsSuspended;
#endif

        public static IReadOnlyList<GRKingCharacterShadowCaster> ActiveCasters => s_ActiveCasters;
        public int CasterId => GetInstanceID();
        private int m_ShadowCasterId;
        // 小整数可以精确地上传到 float；不使用可能超出 float 整数精度的 InstanceID。
        public int ShadowCasterId => m_ShadowCasterId != 0
            ? m_ShadowCasterId : m_ShadowCasterId = s_NextShadowCasterId++;

        [SerializeField, Min(0f)]
        [Tooltip("在世界空间中，向包围盒每一侧额外扩展的距离。")]
        private float m_BoundsPadding = 0.05f;

        private readonly List<Renderer> m_Renderers = new();
        private readonly List<Renderer> m_BoundsRenderers = new();
        private readonly List<Material> m_Materials = new();
        private bool m_RenderersDirty = true;
        private readonly GRKingCharacterShadowMaterials m_ShadowMaterials = new();

        public IReadOnlyList<Renderer> Renderers
        {
            get
            {
                if (m_RenderersDirty)
                    RefreshRenderers();
                return m_Renderers;
            }
        }

        private void OnEnable()
        {
            m_RenderersDirty = true;
            if (gameObject.scene.IsValid() && !s_ActiveCasters.Contains(this))
                s_ActiveCasters.Add(this);
            UpdateShadowMaterials();
        }

        private void OnDisable()
        {
            s_ActiveCasters.Remove(this);
            m_ShadowMaterials.Release();
        }

        private void OnDestroy() => m_ShadowMaterials.Release();

        private void LateUpdate() => UpdateShadowMaterials();

        internal void UpdateShadowMaterials()
        {
            if (!isActiveAndEnabled || !gameObject.scene.IsValid())
                return;
#if UNITY_EDITOR
            if (s_EditorMaterialsSuspended || UnityEditor.EditorApplication.isCompiling ||
                UnityEditor.EditorApplication.isPlaying != UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode ||
                UnityEditor.EditorUtility.IsPersistent(this) ||
                UnityEditor.SceneManagement.EditorSceneManager.IsPreviewScene(gameObject.scene) ||
                UnityEditor.BuildPipeline.isBuildingPlayer)
                return;
#endif
            m_ShadowMaterials.Update(Renderers, ShadowCasterId);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetRegistry()
        {
            s_ActiveCasters.Clear();
            // 关闭 Domain Reload 时角色仍持有旧 ID，不能将分配器重置回 1。
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void RestoreRegistry()
        {
            // 同时兼容关闭 Domain Reload / Scene Reload 的 Enter Play Mode 设置。
            foreach (GRKingCharacterShadowCaster caster in
                     FindObjectsByType<GRKingCharacterShadowCaster>(FindObjectsSortMode.None))
            {
                if (caster.isActiveAndEnabled && caster.gameObject.scene.IsValid() &&
                    !s_ActiveCasters.Contains(caster))
                    s_ActiveCasters.Add(caster);
            }
        }

        private void OnValidate()
        {
            m_BoundsPadding = Mathf.Max(0f, m_BoundsPadding);
            m_RenderersDirty = true;
        }

        private void OnTransformChildrenChanged()
        {
            m_RenderersDirty = true;
        }

#if UNITY_EDITOR
        [UnityEditor.InitializeOnLoadMethod]
        private static void RegisterEditorMaterialCleanup()
        {
            // Domain Reload 可能发生在 Play 切换中间，初始化时也要保持暂停。
            s_EditorMaterialsSuspended = UnityEditor.EditorApplication.isPlaying !=
                UnityEditor.EditorApplication.isPlayingOrWillChangePlaymode;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload -= SuspendEditorMaterials;
            UnityEditor.AssemblyReloadEvents.beforeAssemblyReload += SuspendEditorMaterials;
            UnityEditor.EditorApplication.playModeStateChanged -= OnEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.playModeStateChanged += OnEditorPlayModeStateChanged;
            UnityEditor.EditorApplication.quitting -= SuspendEditorMaterials;
            UnityEditor.EditorApplication.quitting += SuspendEditorMaterials;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving -= OnEditorSceneSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaving += OnEditorSceneSaving;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved -= OnEditorSceneSaved;
            UnityEditor.SceneManagement.EditorSceneManager.sceneSaved += OnEditorSceneSaved;
        }

        private static void OnEditorPlayModeStateChanged(UnityEditor.PlayModeStateChange state)
        {
            if (state == UnityEditor.PlayModeStateChange.ExitingEditMode ||
                state == UnityEditor.PlayModeStateChange.ExitingPlayMode)
                SuspendEditorMaterials();
            else
                ResumeEditorMaterials();
        }

        private static void OnEditorSceneSaving(UnityEngine.SceneManagement.Scene scene, string path)
        {
            SuspendEditorMaterials();
        }

        private static void OnEditorSceneSaved(UnityEngine.SceneManagement.Scene scene)
        {
            ResumeEditorMaterials();
        }

        private static void SuspendEditorMaterials()
        {
            // 必须早于 Play 的场景快照；恢复后禁止 LateUpdate/阴影 Pass 重新绑定副本。
            s_EditorMaterialsSuspended = true;
            RestoreEditorMaterials();
        }

        private static void ResumeEditorMaterials()
        {
            s_EditorMaterialsSuspended = false;
            foreach (GRKingCharacterShadowCaster caster in s_ActiveCasters)
                if (caster != null)
                    caster.UpdateShadowMaterials();
        }

        private static void RestoreEditorMaterials()
        {
            foreach (GRKingCharacterShadowCaster caster in s_ActiveCasters)
                if (caster != null)
                    caster.m_ShadowMaterials.Release();
        }
#endif

        /// <summary>动态添加深层子节点或 Renderer 后，可主动刷新缓存。</summary>
        [ContextMenu("Refresh Renderers")]
        public void RefreshRenderers()
        {
            m_Renderers.Clear();
            GetComponentsInChildren(true, m_Renderers);
            for (int i = m_Renderers.Count - 1; i >= 0; i--)
            {
                if (m_Renderers[i] is not MeshRenderer &&
                    m_Renderers[i] is not SkinnedMeshRenderer)
                    m_Renderers.RemoveAt(i);
            }
            m_RenderersDirty = false;
        }

        /// <summary>只合并有效网格、启用投影且含角色阴影 Pass 的 Renderer。</summary>
        public bool TryGetWorldBounds(out Bounds worldBounds)
        {
            return CollectShadowRenderers(-1, m_BoundsRenderers, out worldBounds);
        }

        public bool CollectShadowRenderers(int layerMask, List<Renderer> result, out Bounds worldBounds)
        {
            result.Clear();
            worldBounds = default;
            bool hasBounds = false;

            foreach (Renderer renderer in Renderers)
            {
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy ||
                    renderer.shadowCastingMode == UnityEngine.Rendering.ShadowCastingMode.Off ||
                    (layerMask & (1 << renderer.gameObject.layer)) == 0)
                    continue;

                Mesh mesh = renderer is SkinnedMeshRenderer skinnedRenderer
                    ? skinnedRenderer.sharedMesh : renderer.GetComponent<MeshFilter>()?.sharedMesh;
                if (mesh == null)
                    continue;

                m_Materials.Clear();
                renderer.GetSharedMaterials(m_Materials);
                bool canCastShadow = false;
                for (int i = 0; i < Mathf.Min(mesh.subMeshCount, m_Materials.Count); i++)
                {
                    if (GRKingShadowUtility.FindShadowCasterPass(m_Materials[i]) >= 0)
                    {
                        canCastShadow = true;
                        break;
                    }
                }
                if (!canCastShadow)
                    continue;

                Bounds bounds = renderer.bounds;
                if (!GRKingShadowUtility.IsValidBounds(bounds))
                    continue;

                result.Add(renderer);
                if (!hasBounds)
                {
                    worldBounds = bounds;
                    hasBounds = true;
                }
                else
                {
                    worldBounds.Encapsulate(bounds);
                }
            }

            if (hasBounds)
                worldBounds.Expand(Mathf.Max(0f, m_BoundsPadding) * 2f);
            return hasBounds;
        }

        private void OnDrawGizmosSelected()
        {
            if (!TryGetWorldBounds(out Bounds bounds))
                return;

            Color previousColor = Gizmos.color;
            Matrix4x4 previousMatrix = Gizmos.matrix;
            Gizmos.color = Color.green;
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
            Gizmos.matrix = previousMatrix;
            Gizmos.color = previousColor;
        }
    }
}
