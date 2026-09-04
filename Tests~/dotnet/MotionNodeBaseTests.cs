using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    [TestFixture]
    public sealed class MotionNodeBaseTests
    {
        private sealed class PlainEffect : MotionEffectNode
        {
            public int PlayCount;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                PlayCount++;
                return MotionHandle.Completed;
            }
        }

        private sealed class OwningFlow : MotionFlowNode
        {
            public override bool OwnsChildren => true;

            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }

        private sealed class NullReturningEffect : MotionEffectNode
        {
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return null;
            }
        }

        [Test]
        public void EffectNode_DoesNotOwnChildren_ByDefault()
        {
            // 효과 노드의 자식은 효과가 끝난 뒤 실행기가 이어 붙인다.
            Assert.That(new PlainEffect().OwnsChildren, Is.False);
        }

        [Test]
        public void EffectNode_DoesNotRevert_ByDefault()
        {
            Assert.That(new PlainEffect().Reverts, Is.False);
        }

        [Test]
        public void FlowNode_DoesNotOwnChildren_ByDefault()
        {
            // Delay처럼 자식을 실행기에 맡기는 흐름 노드가 있다.
            Assert.That(new PlainFlow().OwnsChildren, Is.False);
        }

        [Test]
        public void FlowNode_CanOwnChildren()
        {
            Assert.That(new OwningFlow().OwnsChildren, Is.True);
        }

        [Test]
        public void Play_DelegatesToOnPlay()
        {
            var node = new PlainEffect();
            IMotionHandle handle = node.Play(null);

            Assert.That(node.PlayCount, Is.EqualTo(1));
            Assert.That(handle.IsDone, Is.True);
        }

        [Test]
        public void Play_NeverReturnsNull()
        {
            // 노드가 실수로 null을 돌려줘도 실행기가 죽으면 안 된다.
            var node = new NullReturningEffect();
            IMotionHandle handle = node.Play(null);

            Assert.That(handle, Is.Not.Null);
            Assert.That(handle.IsDone, Is.True);
        }

        [Test]
        public void Id_DefaultsToNone()
        {
            Assert.That(new PlainEffect().Id, Is.EqualTo(NodeId.None));
        }

        [Test]
        public void Id_IsPublicAndAssignable()
        {
            // 그래프가 노드에 id를 부여한다. 직렬화되므로 public 필드여야 한다.
            var node = new PlainEffect();
            node.Id = new NodeId(3);
            Assert.That(node.Id.Value, Is.EqualTo(3));
        }

        private sealed class PlainFlow : MotionFlowNode
        {
            protected override IMotionHandle OnPlay(IMotionContext ctx)
            {
                return MotionHandle.Completed;
            }
        }
    }
}
