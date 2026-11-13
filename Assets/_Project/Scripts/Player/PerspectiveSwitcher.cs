using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>1인칭·3인칭 카메라를 바꿀 수 있는 캐릭터.</summary>
    public interface IPerspectiveCamera
    {
        bool IsFirstPerson { get; }

        /// <summary>카메라 배치가 캐릭터마다 달라서 전환 방법을 캐릭터가 정한다.</summary>
        void SetFirstPerson(bool firstPerson);
    }

    /// <summary>시점이 바뀔 때 본인 화면에 보이는 것을 맞추는 구성 요소.</summary>
    public interface IPerspectiveListener
    {
        /// <summary>전환기가 구성 요소 종류를 몰라도 되도록, 시점이 바뀌면 각자 보이는 것을 맞추게 알린다.</summary>
        void OnPerspectiveChanged(bool firstPerson);
    }

    /// <summary>테스트 방에서 로컬 아바타의 1인칭 ↔ 3인칭을 바꾼다.</summary>
    public static class PerspectiveSwitcher
    {
        /// <summary>1인칭에서 자기 몸이 카메라를 가리지 않도록 그림자만 남긴다.</summary>
        public static bool Toggle(Component avatar)
        {
            if (avatar == null || !avatar.TryGetComponent(out IPerspectiveCamera perspective))
                return false;

            bool firstPerson = !perspective.IsFirstPerson;
            perspective.SetFirstPerson(firstPerson);

            ShadowCastingMode bodyMode = firstPerson ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
            foreach (Renderer body in CharacterSkinView.ResolveBodyRenderers(avatar, null))
            {
                if (body != null)
                    body.shadowCastingMode = bodyMode;
            }

            foreach (IPerspectiveListener listener in avatar.GetComponentsInChildren<IPerspectiveListener>(true))
                listener.OnPerspectiveChanged(firstPerson);
            return true;
        }
    }
}
