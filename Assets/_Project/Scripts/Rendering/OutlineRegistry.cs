using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Rendering
{
    /// <summary>외곽선 하나: 그릴 렌더러들과 종류.</summary>
    public readonly struct OutlineEntry
    {
        public readonly Renderer[] Renderers;
        public readonly OutlineKind Kind;

        /// <summary>렌더러 기능이 요청자를 몰라도 그릴 수 있도록 그릴 것만 묶는다.</summary>
        public OutlineEntry(Renderer[] renderers, OutlineKind kind)
        {
            Renderers = renderers;
            Kind = kind;
        }
    }

    /// <summary>렌더러 기능이 매 프레임 읽을 지금 표시할 외곽선 목록.</summary>
    public interface IOutlineSource
    {
        IEnumerable<OutlineEntry> Entries { get; }
    }

    /// <summary>게임 코드와 렌더러 기능이 서로 모르도록 외곽선 요청을 모아 두는 기본 구현.</summary>
    public sealed class OutlineRegistry : IOutlineService, IOutlineSource
    {
        /// <summary>씬·프리팹에서 렌더러 기능을 직렬화 참조할 수 없어서 게임 전체가 하나의 목록을 정적으로 공유한다.</summary>
        public static OutlineRegistry Instance { get; } = new();

        private readonly Dictionary<Object, OutlineEntry> entries = new();
        // 반복 중에 사전을 고칠 수 없어서 지울 요청자를 모아 두는 버퍼
        private readonly List<Object> deadOwners = new();

        /// <inheritdoc />
        public void Show(Object owner, Renderer[] renderers, OutlineKind kind)
        {
            if (owner == null)
                return;
            // 빈 대상으로 부르면 그릴 것이 없으므로 숨기는 것과 같게 처리한다.
            if (renderers == null || renderers.Length == 0)
            {
                entries.Remove(owner);
                return;
            }
            entries[owner] = new OutlineEntry(renderers, kind);
        }

        /// <inheritdoc />
        public void Hide(Object owner)
        {
            if (owner != null)
                entries.Remove(owner);
        }

        /// <inheritdoc />
        public IEnumerable<OutlineEntry> Entries
        {
            get
            {
                RemoveDestroyedOwners();
                return entries.Values;
            }
        }

        /// <summary>
        /// Hide를 부르지 못하고 파괴된 요청자의 외곽선이 영원히 남지 않도록 조회할 때마다 정리한다.
        /// </summary>
        private void RemoveDestroyedOwners()
        {
            deadOwners.Clear();
            foreach (Object owner in entries.Keys)
            {
                if (owner == null)
                    deadOwners.Add(owner);
            }
            foreach (Object owner in deadOwners)
                entries.Remove(owner);
        }

        /// <summary>
        /// 도메인 재로드를 끈 경우 정적 목록에 이전 플레이의 요청이 남으므로 플레이 모드에 들어갈 때 비운다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnPlay()
        {
            Instance.entries.Clear();
        }
    }
}
