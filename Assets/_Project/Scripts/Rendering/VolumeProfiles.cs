using System;
using UnityEngine;
using UnityEngine.Rendering;

namespace GuardVsMannequin.Rendering
{
    /// <summary>미리 만든 볼륨 프로필 에셋과 실행 중 기본 프로필을 같은 방식으로 다루도록 모은 도우미.</summary>
    public static class VolumeProfiles
    {
        /// <summary>
        /// 실행 중에 값을 바꾸므로 에셋은 복사본으로 쓴다(에디터 플레이에서 에셋이 바뀌지 않게). 에셋이 없으면 기본 프로필을 만든다.
        /// 돌려준 프로필은 부르는 쪽이 Release로 지운다.
        /// </summary>
        public static VolumeProfile Assign(Volume volume, VolumeProfile template, Func<VolumeProfile> createDefault)
        {
            if (template == null)
            {
                VolumeProfile created = createDefault();
                volume.sharedProfile = created;
                return created;
            }

            volume.sharedProfile = template;
            return volume.profile;
        }

        /// <summary>사용자가 에셋에서 항목을 지웠어도 효과가 동작하도록 없으면 추가한다.</summary>
        public static T GetOrAdd<T>(VolumeProfile profile) where T : VolumeComponent
        {
            return profile.TryGet(out T component) ? component : profile.Add<T>(true);
        }

        /// <summary>프로필과 그 안의 효과는 런타임 객체라 자동으로 해제되지 않으므로 함께 지운다.</summary>
        public static void Release(VolumeProfile profile)
        {
            if (profile == null)
                return;
            foreach (VolumeComponent component in profile.components)
            {
                if (component != null)
                    UnityEngine.Object.Destroy(component);
            }
            UnityEngine.Object.Destroy(profile);
        }
    }
}
