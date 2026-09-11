using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading;

namespace Juahn.UiMotion
{
    public enum MotionOutcome { Completed, Canceled, Skipped, Failed }

    public readonly struct MotionResult
    {
        public readonly long RunId;
        public readonly MotionOutcome Outcome;
        public readonly Exception Error;
        public MotionResult(long runId, MotionOutcome outcome, Exception error = null)
        { RunId = runId; Outcome = outcome; Error = error; }
    }

    /// <summary>발사 요청 하나의 결과. 그래프나 다음 발사의 상태와 독립적이다.</summary>
    public sealed class MotionPlayback
    {
        private static long _nextId;
        private Action<MotionResult> _completed;
        public long RunId { get; } = Interlocked.Increment(ref _nextId);
        public bool IsDone { get; private set; }
        public MotionResult Result { get; private set; }
        public IReadOnlyDictionary<string, float> Parameters { get; }

        internal MotionPlayback(IReadOnlyDictionary<string, float> parameters = null)
        {
            if (parameters == null) { Parameters = EmptyParameters; return; }
            var snapshot = new Dictionary<string, float>();
            foreach (var pair in parameters) snapshot.Add(pair.Key, pair.Value);
            Parameters = new ReadOnlyDictionary<string, float>(snapshot);
        }
        internal static readonly IReadOnlyDictionary<string, float> EmptyParameters =
            new ReadOnlyDictionary<string, float>(new Dictionary<string, float>());

        public void WhenCompleted(Action<MotionResult> callback)
        {
            if (callback == null) return;
            if (IsDone) callback(Result); else _completed += callback;
        }
        internal void Complete(MotionOutcome outcome, Exception error = null)
        {
            if (IsDone) return;
            IsDone = true;
            Result = new MotionResult(RunId, outcome, error);
            var callbacks = _completed; _completed = null;
            callbacks?.Invoke(Result);
        }
        public static MotionPlayback Skipped()
        {
            var playback = new MotionPlayback(); playback.Complete(MotionOutcome.Skipped); return playback;
        }
    }
}
