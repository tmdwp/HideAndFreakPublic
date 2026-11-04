using FishNet.Object;
using GuardVsMannequin.Settings;
using UnityEngine;

namespace GuardVsMannequin.Audio
{
    /// <summary>
    /// 오브젝트 위치에서 3D 소음을 재생한다.
    /// 서버가 <see cref="Emit"/>으로 모든 클라이언트에 재생을 지시하고,
    /// 이미 RPC로 전달된 사건(사격 등)은 <see cref="PlayLocal"/>로 추가 전송 없이 재생한다.
    /// </summary>
    public class NoiseEmitter : NetworkBehaviour
    {
        [SerializeField] private NoiseLibrary library;

        private AudioSource audioSource;

        /// <summary>소음 설정. (반복 재생하는 소리를 직접 관리하는 구성 요소가 클립·거리를 읽는다)</summary>
        public NoiseLibrary Library => library;

        /// <summary>
        /// 3D 오디오 소스를 준비한다.
        /// </summary>
        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1f;
        }

        /// <summary>
        /// 모든 클라이언트에서 소음을 재생한다. (서버 전용)
        /// </summary>
        [Server]
        public void Emit(NoiseType type)
        {
            EmitObserversRpc(type);
        }

        /// <summary>
        /// 서버 지시를 받아 소음을 재생한다.
        /// </summary>
        [ObserversRpc]
        private void EmitObserversRpc(NoiseType type)
        {
            PlayLocal(type);
        }

        /// <summary>
        /// 이 클라이언트에서만 소음을 재생한다.
        /// </summary>
        /// <param name="volumeScale">설정 크기에 곱할 배율. (본인에게 들리는 자기 몸 소리를 줄일 때 등)</param>
        public void PlayLocal(NoiseType type, float volumeScale = 1f)
        {
            if (library == null || audioSource == null)
                return;

            NoiseLibrary.Entry entry = library.Get(type);
            audioSource.maxDistance = entry.maxDistance;
            audioSource.PlayOneShot(library.GetClip(type), entry.volume * volumeScale * AudioVolumes.Sfx);
        }

        /// <summary>
        /// 이 클라이언트에서만, 오브젝트가 아닌 지정한 위치에서 소음을 재생한다. (수류탄 폭발처럼 떨어진 곳의 소리)
        /// 재생이 끝나면 임시 오디오 오브젝트를 지운다.
        /// </summary>
        public void PlayLocalAt(NoiseType type, Vector3 position)
        {
            if (library == null || audioSource == null)
                return;

            NoiseLibrary.Entry entry = library.Get(type);
            AudioClip clip = library.GetClip(type);
            var holder = new GameObject($"Noise_{type}");
            holder.transform.position = position;

            AudioSource source = holder.AddComponent<AudioSource>();
            source.spatialBlend = audioSource.spatialBlend;
            source.rolloffMode = audioSource.rolloffMode;
            source.minDistance = audioSource.minDistance;
            source.outputAudioMixerGroup = audioSource.outputAudioMixerGroup;
            source.maxDistance = entry.maxDistance;
            source.PlayOneShot(clip, entry.volume * AudioVolumes.Sfx);
            Destroy(holder, clip.length + 0.1f);
        }
    }
}
