using UnityEngine;

    public sealed class UnityAudioGameplayAudioSystem : IGameplayAudioSystem
    {
        public void PlayBgm(string key)
        {
#if UNITY_EDITOR
            Debug.Log($"[GameplayAudio] PlayBgm: {key}");
#endif
        }

        public void StopBgm()
        {
#if UNITY_EDITOR
            Debug.Log("[GameplayAudio] StopBgm");
#endif
        }
    }
