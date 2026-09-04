using System.Collections.Generic;
using UnityEngine;

namespace Juahn.UiMotion
{
    /// <summary>
    /// 살아 있는 모든 플레이어를 한 <c>Update</c>에서 굴리는 전역 펌프.
    ///
    /// <b>왜 플레이어마다 Update를 두지 않는가</b> — 방치형 게임이라 인벤토리 슬롯 수백 개가
    /// 동시에 산다. <c>MonoBehaviour.Update</c>는 호출마다 네이티브에서 매니지드로 넘어오므로
    /// 300개면 그 비용이 300배다. 펌프는 1회다.
    ///
    /// 에디터에서 플레이 중이 아닐 때는 만들지 않는다. 씬에 숨은 오브젝트를 남기면 안 되기
    /// 때문이다. 에디터 프리뷰는 <see cref="MotionPlayer.TickFromPump"/>를 직접 부른다.
    /// </summary>
    [AddComponentMenu("")]
    [DisallowMultipleComponent]
    public sealed class MotionPump : MonoBehaviour
    {
        private static MotionPump _instance;
        private static bool _quitting;

        private readonly List<MotionPlayer> _players = new List<MotionPlayer>();
        private bool _ticking;
        private bool _needsCompact;

        public static void Register(MotionPlayer player)
        {
            if (player == null || !Application.isPlaying || _quitting)
            {
                return;
            }

            MotionPump pump = EnsureInstance();
            if (pump == null || pump._players.Contains(player))
            {
                return;
            }

            pump._players.Add(player);
        }

        public static void Unregister(MotionPlayer player)
        {
            if (_instance == null || player == null)
            {
                return;
            }

            List<MotionPlayer> players = _instance._players;
            int index = players.IndexOf(player);
            if (index < 0)
            {
                return;
            }

            // 틱 도중에 목록을 줄이면 인덱스가 어긋난다. 자리를 비워 두고 나중에 정리한다.
            if (_instance._ticking)
            {
                players[index] = null;
                _instance._needsCompact = true;
                return;
            }

            players.RemoveAt(index);
        }

        private static MotionPump EnsureInstance()
        {
            if (_instance != null)
            {
                return _instance;
            }

            var go = new GameObject("[UiMotion Pump]");
            go.hideFlags = HideFlags.HideAndDontSave;
            DontDestroyOnLoad(go);

            _instance = go.AddComponent<MotionPump>();
            return _instance;
        }

        private void Update()
        {
            float unscaled = Time.unscaledDeltaTime;
            float scaled = Time.deltaTime;

            _ticking = true;

            // 틱 도중에 플레이어가 비활성화되거나 파괴될 수 있으므로 매번 다시 검사한다.
            for (int i = 0; i < _players.Count; i++)
            {
                MotionPlayer player = _players[i];

                if (player == null)
                {
                    _players[i] = null;
                    _needsCompact = true;
                    continue;
                }

                player.TickFromPump(unscaled, scaled);
            }

            _ticking = false;

            if (_needsCompact)
            {
                Compact();
            }
        }

        private void Compact()
        {
            _needsCompact = false;

            for (int i = _players.Count - 1; i >= 0; i--)
            {
                if (_players[i] == null)
                {
                    _players.RemoveAt(i);
                }
            }
        }

        private void OnApplicationQuit()
        {
            // 종료 중에 펌프를 되살리면 "파괴된 씬에 오브젝트를 만들 수 없다" 오류가 난다.
            _quitting = true;
        }

        private void OnDestroy()
        {
            if (ReferenceEquals(_instance, this))
            {
                _instance = null;
            }
        }
    }
}
