using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Player
{
    /// <summary>
    /// 1인칭/3인칭 카메라를 바꿀 수 있는 캐릭터. (가드, 마네킹)
    /// </summary>
    public interface IPerspectiveCamera
    {
        /// <summary>1인칭 시점인지 여부.</summary>
        bool IsFirstPerson { get; }

        /// <summary>1인칭/3인칭 카메라로 바꾼다.</summary>
        void SetFirstPerson(bool firstPerson);
    }

    /// <summary>
    /// 소유자 시점이 바뀔 때 본인 화면의 보이는 것(1인칭 총, 변신 사물 등)을 맞추는 구성 요소.
    /// </summary>
    public interface IPerspectiveListener
    {
        /// <summary>소유자 시점이 바뀌었을 때 호출된다.</summary>
        void OnPerspectiveChanged(bool firstPerson);
    }

    /// <summary>
    /// 로컬 아바타의 1인칭 ↔ 3인칭을 바꾼다. (테스트 방)
    /// 카메라는 <see cref="IPerspectiveCamera"/>가, 몸은 여기서(1인칭: 그림자만), 나머지는 <see cref="IPerspectiveListener"/>가 맞춘다.
    /// </summary>
    public static class PerspectiveSwitcher
    {
        /// <summary>
        /// 아바타의 시점을 반대로 바꾼다. 바꿀 수 없는 아바타면 false.
        /// </summary>
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
