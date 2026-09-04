using System;
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

        /// <summary>
        /// 플레이 시작마다 static 상태를 되돌린다.
        ///
        /// <b>왜 필요한가</b> — Enter Play Mode Options로 도메인 리로드를 끄면 static이
        /// 플레이 세션을 넘어 살아남는다. 지난 세션에서 <see cref="_quitting"/>이 true가 된 채
        /// 남으면 펌프가 다시 만들어지지 않아 <b>모든 연출이 조용히 멈춘다</b>. 두 번째로
        /// 플레이를 누른 순간부터 아무것도 움직이지 않는데 오류는 하나도 나오지 않는다.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
            _quitting = false;
        }

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

            // 순회 길이를 먼저 굳힌다. 연출 노드가 다른 오브젝트를 켜면 그 플레이어의
            // OnEnable이 이 루프 도중에 Register를 부르는데, 길이를 매번 다시 읽으면
            // 새 플레이어가 등록된 바로 그 프레임에 델타를 한 번 먹는다. OnEnable에서
            // 이미 Start가 발사됐으므로 그만큼 한 프레임 앞서 가고, 순차로 튀어나오는
            // 목록 연출에서 간격이 실제로 어긋난다.
            int count = _players.Count;

            // try/finally인 이유 — 순회가 예외로 빠져나가면 _ticking이 true로 굳는다.
            // 그러면 Compact()가 영영 실행되지 않아 목록이 null로 무한히 자란다.
            // 펌프는 하나뿐이므로 그 손상은 게임 전체 UI에 남는다.
            try
            {
                // 틱 도중에 플레이어가 비활성화되거나 파괴될 수 있으므로 매번 다시 검사한다.
                for (int i = 0; i < count; i++)
                {
                    MotionPlayer player = _players[i];

                    if (player == null)
                    {
                        _players[i] = null;
                        _needsCompact = true;
                        continue;
                    }

                    try
                    {
                        player.TickFromPump(unscaled, scaled);
                    }
                    catch (Exception error)
                    {
                        DetachFailed(i, player, error);
                    }
                }
            }
            finally
            {
                _ticking = false;

                if (_needsCompact)
                {
                    Compact();
                }
            }
        }

        /// <summary>
        /// 틱이 던진 플레이어를 펌프에서 떼어낸다.
        ///
        /// <b>시끄럽게 한 번 실패하고 멈추는 편이 조용히 전체를 죽이는 것보다 낫다.</b>
        /// 예외를 그대로 흘리면 뒤 인덱스의 플레이어가 그 프레임에 전부 멈춘다.
        /// 떼어내지 않으면 같은 예외가 매 프레임 콘솔을 채워 진짜 문제를 덮는다.
        /// 로그에 플레이어를 문맥으로 달아 계층에서 바로 찾을 수 있게 한다.
        /// </summary>
        private void DetachFailed(int index, MotionPlayer player, Exception error)
        {
            Debug.LogException(error, player);

            // 복구가 또 던질 수 있다. 그것 때문에 떼어내기를 못 하면 원래 문제로 돌아간다.
            try
            {
                player.StopAll();
            }
            catch (Exception stopError)
            {
                Debug.LogException(stopError, player);
            }

            // 틱 도중이므로 목록을 줄이지 않는다. 자리를 비워 두고 Compact가 정리한다.
            _players[index] = null;
            _needsCompact = true;
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
