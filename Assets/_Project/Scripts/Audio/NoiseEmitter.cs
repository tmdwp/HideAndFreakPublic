using FishNet.Object;
using GuardVsMannequin.Settings;
using UnityEngine;

namespace GuardVsMannequin.Audio
{
    /// <summary>오브젝트 위치에서 3D 소음을 재생하며, 이미 RPC로 전달된 사건은 추가 전송 없이 재생한다.</summary>
    public class NoiseEmitter : NetworkBehaviour
    {
        [SerializeField] private NoiseLibrary library;

        private AudioSource audioSource;

        /// <summary>반복 재생하는 소리를 직접 관리하는 구성 요소가 같은 클립·거리를 쓰도록 연다.</summary>
        public NoiseLibrary Library => library;

        /// <summary>
        /// 거리에 따라 크기가 줄어야 위치 단서가 되므로 3D 소스를 선형 감쇠로 준비한다.
        /// </summary>
        private void Awake()
        {
            audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            audioSource.spatialBlend = 1f;
            audioSource.rolloffMode = AudioRolloffMode.Linear;
            audioSource.minDistance = 1f;
        }

        /// <summary>서버에서만 일어나는 사건을 모든 클라이언트가 듣도록 재생을 지시한다.</summary>
        [Server]
        public void Emit(NoiseType type)
        {
            EmitObserversRpc(type);
        }

        /// <summary>
        /// 실제 재생은 각 클라이언트가 하므로 받은 종류를 로컬로 재생한다.
        /// </summary>
        [ObserversRpc]
        private void EmitObserversRpc(NoiseType type)
        {
            PlayLocal(type);
        }

        /// <summary>
        /// 사격처럼 이미 RPC로 모두에게 알려진 사건은 소음을 따로 보낼 필요가 없어서 이 클라이언트에서만 재생한다.
        /// </summary>
        /// <param name="volumeScale">본인에게 들리는 자기 몸 소리처럼 줄여야 할 때 곱할 배율.</param>
        public void PlayLocal(NoiseType type, float volumeScale = 1f)
        {
            if (library == null || audioSource == null)
                return;

            NoiseLibrary.Entry entry = library.Get(type);
            audioSource.maxDistance = entry.maxDistance;
            audioSource.PlayOneShot(library.GetClip(type), entry.volume * volumeScale * AudioVolumes.Sfx);
        }

        /// <summary>오브젝트와 떨어진 곳의 소리가 그 위치에서 들리도록 같은 설정의 임시 오디오 오브젝트로 재생한다.</summary>
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
