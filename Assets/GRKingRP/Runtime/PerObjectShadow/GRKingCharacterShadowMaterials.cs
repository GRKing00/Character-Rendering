using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace GRKingRP.PerObjectShadow
{
    /// <summary>每个角色缓存自己的接收材质；不使用 MPB，也不修改材质资产。</summary>
    internal sealed class GRKingCharacterShadowMaterials
    {
        private static readonly int s_CasterId = Shader.PropertyToID("_GRKingShadowCasterId");

        private sealed class Binding
        {
            public Renderer Renderer;
            public Material[] Originals;
            public Material[] Assigned;
        }

        private readonly List<Binding> m_Bindings = new();
        private readonly List<Material> m_CurrentMaterials = new();
        private readonly Dictionary<Material, Material> m_Copies = new();
        private readonly Dictionary<Material, Material> m_OriginalByCopy = new();

        public void Update(IReadOnlyList<Renderer> renderers, int casterId)
        {
            if (!BindingsMatch(renderers))
            {
                Release();
                foreach (Renderer renderer in renderers)
                {
                    m_CurrentMaterials.Clear();
                    if (renderer != null)
                        renderer.GetSharedMaterials(m_CurrentMaterials);
                    Material[] originals = m_CurrentMaterials.ToArray();
                    Material[] assigned = (Material[])originals.Clone();
                    bool changed = false;
                    for (int slot = 0; slot < originals.Length; slot++)
                    {
                        Material original = originals[slot];
                        if (original == null || !original.HasProperty(s_CasterId))
                            continue;
                        if (!m_Copies.TryGetValue(original, out Material copy))
                        {
                            copy = new Material(original)
                            {
                                name = original.name + " (GRKing Shadow " + casterId + ")",
                                hideFlags = HideFlags.HideAndDontSave
                            };
                            copy.SetFloat(s_CasterId, casterId);
                            m_Copies.Add(original, copy);
                            m_OriginalByCopy.Add(copy, original);
                        }
                        assigned[slot] = copy;
                        changed = true;
                    }
                    if (changed)
                        renderer.sharedMaterials = assigned;
                    m_Bindings.Add(new Binding { Renderer = renderer, Originals = originals, Assigned = assigned });
                }
            }

#if UNITY_EDITOR
            if (!Application.isPlaying)
            {
                // 编辑器调原材质时同步副本；正式运行只在绑定时写 ID。
                foreach (KeyValuePair<Material, Material> pair in m_Copies)
                {
                    if (pair.Key == null || pair.Value == null)
                        continue;
                    pair.Value.shader = pair.Key.shader;
                    pair.Value.CopyPropertiesFromMaterial(pair.Key);
                    pair.Value.SetFloat(s_CasterId, casterId);
                }
            }
#endif
        }

        private bool BindingsMatch(IReadOnlyList<Renderer> renderers)
        {
            if (m_Bindings.Count != renderers.Count)
                return false;
            for (int index = 0; index < renderers.Count; index++)
            {
                Binding binding = m_Bindings[index];
                if (binding.Renderer != renderers[index])
                    return false;
                if (binding.Renderer == null)
                    continue;
                m_CurrentMaterials.Clear();
                binding.Renderer.GetSharedMaterials(m_CurrentMaterials);
                if (m_CurrentMaterials.Count != binding.Assigned.Length)
                    return false;
                for (int slot = 0; slot < binding.Assigned.Length; slot++)
                {
                    if (m_CurrentMaterials[slot] != binding.Assigned[slot])
                        return false;
                    // Unity 的已销毁对象也会与 null 相等，不能把失效副本当成正常绑定。
                    Material assigned = binding.Assigned[slot];
                    if (!ReferenceEquals(assigned, null) && assigned == null &&
                        !ReferenceEquals(assigned, binding.Originals[slot]))
                        return false;
                    Material original = binding.Originals[slot];
                    bool needsCopy = original != null && original.HasProperty(s_CasterId);
                    if (needsCopy != (binding.Assigned[slot] != original))
                        return false;
                }
            }
            return true;
        }

        public void Release()
        {
            foreach (Binding binding in m_Bindings)
            {
                if (binding.Renderer == null)
                    continue;
                m_CurrentMaterials.Clear();
                binding.Renderer.GetSharedMaterials(m_CurrentMaterials);
                bool changed = false;
                for (int slot = 0; slot < m_CurrentMaterials.Count; slot++)
                {
                    Material current = m_CurrentMaterials[slot];
                    // 只恢复自己创建的副本，保留用户在运行期间重新指定的材质。
                    if (!ReferenceEquals(current, null) &&
                        m_OriginalByCopy.TryGetValue(current, out Material original))
                    {
                        m_CurrentMaterials[slot] = original;
                        changed = true;
                    }
                    else if (current == null && slot < binding.Assigned.Length &&
                        !ReferenceEquals(binding.Assigned[slot], null) && binding.Assigned[slot] == null &&
                        !ReferenceEquals(binding.Assigned[slot], binding.Originals[slot]) &&
                        binding.Originals[slot] != null)
                    {
                        m_CurrentMaterials[slot] = binding.Originals[slot];
                        changed = true;
                    }
                }
                if (changed)
                {
                    binding.Renderer.sharedMaterials = m_CurrentMaterials.ToArray();
#if UNITY_EDITOR
                    // 更新 Prefab 覆盖缓存，否则销毁副本后可能把旧覆盖序列化为 null。
                    if (!Application.isPlaying &&
                        UnityEditor.PrefabUtility.IsPartOfPrefabInstance(binding.Renderer))
                        UnityEditor.PrefabUtility.RecordPrefabInstancePropertyModifications(binding.Renderer);
#endif
                }
            }
            foreach (Material copy in m_Copies.Values)
                CoreUtils.Destroy(copy);
            m_Bindings.Clear();
            m_Copies.Clear();
            m_OriginalByCopy.Clear();
            m_CurrentMaterials.Clear();
        }
    }
}
