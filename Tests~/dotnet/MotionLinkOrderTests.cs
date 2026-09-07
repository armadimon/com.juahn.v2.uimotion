using System.Collections.Generic;
using Juahn.UiMotion;
using NUnit.Framework;

namespace Juahn.UiMotion.Tests
{
    /// <summary>
    /// 자식 순서를 전역 간선 배열의 인덱스로 옮기는 변환.
    ///
    /// 이것이 틀리면 <c>Sequence</c>의 실행 순서가 조용히 바뀐다 — 오류도 경고도 나지 않고
    /// 연출만 이상해진다. 순수 로직이므로 여기서 덮는다.
    /// </summary>
    [TestFixture]
    public sealed class MotionLinkOrderTests
    {
        private static readonly NodeId P = new NodeId(1);
        private static readonly NodeId Q = new NodeId(2);
        private static readonly NodeId X = new NodeId(10);
        private static readonly NodeId Y = new NodeId(11);
        private static readonly NodeId W = new NodeId(12);
        private static readonly NodeId Z = new NodeId(13);

        /// <summary>P의 간선 사이에 다른 부모의 간선이 끼어 있는 배열. 실제로 흔한 모양이다.</summary>
        private static List<NodeLink> Interleaved()
        {
            return new List<NodeLink>
            {
                new NodeLink(P, X),   // 0  P의 자식 0
                new NodeLink(Q, Z),   // 1
                new NodeLink(P, Y),   // 2  P의 자식 1
                new NodeLink(P, W),   // 3  P의 자식 2
            };
        }

        /// <summary><see cref="MotionGraph.MoveLink"/>와 같은 동작 — 뽑아서 끼워 넣는다.</summary>
        private static void ApplyMove(List<NodeLink> links, int from, int to)
        {
            NodeLink link = links[from];
            links.RemoveAt(from);
            links.Insert(to, link);
        }

        private static List<NodeId> ChildrenOf(List<NodeLink> links, NodeId parent)
        {
            var children = new List<NodeId>();
            for (int i = 0; i < links.Count; i++)
            {
                if (links[i].From == parent)
                {
                    children.Add(links[i].To);
                }
            }

            return children;
        }

        /// <summary>자식 순서를 바꾸고 결과 순서를 돌려준다.</summary>
        private static List<NodeId> Reorder(int childFrom, int childTo)
        {
            List<NodeLink> links = Interleaved();

            int globalFrom;
            int globalTo;
            bool ok = MotionLinkOrder.Resolve(links, P, childFrom, childTo, out globalFrom, out globalTo);
            Assert.That(ok, Is.True);

            ApplyMove(links, globalFrom, globalTo);
            return ChildrenOf(links, P);
        }

        [Test]
        public void IndicesOf_FindsOnlyThatParentsLinks()
        {
            var into = new List<int>();
            MotionLinkOrder.IndicesOf(Interleaved(), P, into);

            Assert.That(into, Is.EqualTo(new[] { 0, 2, 3 }));
        }

        [Test]
        public void IndicesOf_ClearsTargetList()
        {
            var into = new List<int> { 99 };
            MotionLinkOrder.IndicesOf(Interleaved(), Q, into);

            Assert.That(into, Is.EqualTo(new[] { 1 }));
        }

        [Test]
        public void IndicesOf_UnknownParent_IsEmpty()
        {
            var into = new List<int>();
            MotionLinkOrder.IndicesOf(Interleaved(), new NodeId(99), into);

            Assert.That(into, Is.Empty);
        }

        [Test]
        public void IndicesOf_NullLinks_IsSafe()
        {
            var into = new List<int>();
            Assert.DoesNotThrow(delegate { MotionLinkOrder.IndicesOf(null, P, into); });
            Assert.That(into, Is.Empty);
        }

        // --- 실제 재정렬 ----------------------------------------------------

        [Test]
        public void MoveFirstToLast()
        {
            Assert.That(Reorder(0, 2), Is.EqualTo(new[] { Y, W, X }));
        }

        [Test]
        public void MoveLastToFirst()
        {
            Assert.That(Reorder(2, 0), Is.EqualTo(new[] { W, X, Y }));
        }

        [Test]
        public void MoveMiddleDown()
        {
            Assert.That(Reorder(1, 2), Is.EqualTo(new[] { X, W, Y }));
        }

        [Test]
        public void MoveMiddleUp()
        {
            Assert.That(Reorder(1, 0), Is.EqualTo(new[] { Y, X, W }));
        }

        [Test]
        public void MoveToSamePlace_ChangesNothing()
        {
            Assert.That(Reorder(1, 1), Is.EqualTo(new[] { X, Y, W }));
        }

        [Test]
        public void OtherParentsLinksAreUntouched()
        {
            List<NodeLink> links = Interleaved();

            int globalFrom;
            int globalTo;
            MotionLinkOrder.Resolve(links, P, 0, 2, out globalFrom, out globalTo);
            ApplyMove(links, globalFrom, globalTo);

            Assert.That(ChildrenOf(links, Q), Is.EqualTo(new[] { Z }), "다른 부모의 배선이 흔들리면 안 된다");
        }

        // --- 경계 ------------------------------------------------------------

        [Test]
        public void Resolve_OutOfRange_Fails()
        {
            List<NodeLink> links = Interleaved();

            int a;
            int b;
            Assert.That(MotionLinkOrder.Resolve(links, P, -1, 0, out a, out b), Is.False);
            Assert.That(MotionLinkOrder.Resolve(links, P, 0, 3, out a, out b), Is.False);
            Assert.That(MotionLinkOrder.Resolve(links, P, 3, 0, out a, out b), Is.False);
        }

        [Test]
        public void Resolve_SingleChild_Fails()
        {
            var links = new List<NodeLink> { new NodeLink(P, X) };

            int a;
            int b;
            Assert.That(MotionLinkOrder.Resolve(links, P, 0, 0, out a, out b), Is.True);
            Assert.That(a, Is.EqualTo(0));
            Assert.That(b, Is.EqualTo(0));
        }

        [Test]
        public void Resolve_UnknownParent_Fails()
        {
            int a;
            int b;
            Assert.That(MotionLinkOrder.Resolve(Interleaved(), new NodeId(99), 0, 0, out a, out b), Is.False);
        }

        [Test]
        public void ContiguousLinks_AlsoWork()
        {
            var links = new List<NodeLink>
            {
                new NodeLink(P, X),
                new NodeLink(P, Y),
                new NodeLink(P, W),
            };

            int globalFrom;
            int globalTo;
            MotionLinkOrder.Resolve(links, P, 2, 0, out globalFrom, out globalTo);
            ApplyMove(links, globalFrom, globalTo);

            Assert.That(ChildrenOf(links, P), Is.EqualTo(new[] { W, X, Y }));
        }

        [Test]
        public void IndicesOf_SkipsInvalidLinks()
        {
            // MotionGraphIndex.GetChildren도 잘못된 간선을 건너뛴다. 둘이 다르게 세면
            // 화면의 자식 순서와 이 목록이 어긋나 엉뚱한 간선을 옮기게 된다.
            var links = new List<NodeLink>
            {
                new NodeLink(P, X),
                new NodeLink(P, NodeId.None),   // 손상된 에셋에서 나올 수 있다
                new NodeLink(P, Y),
            };

            var into = new List<int>();
            MotionLinkOrder.IndicesOf(links, P, into);

            Assert.That(into, Is.EqualTo(new[] { 0, 2 }));
        }

        [Test]
        public void Resolve_WithInvalidLinkPresent_MovesTheRightOne()
        {
            var links = new List<NodeLink>
            {
                new NodeLink(P, X),
                new NodeLink(P, NodeId.None),
                new NodeLink(P, Y),
            };

            int globalFrom;
            int globalTo;
            Assert.That(MotionLinkOrder.Resolve(links, P, 1, 0, out globalFrom, out globalTo), Is.True);

            ApplyMove(links, globalFrom, globalTo);

            // 유효한 자식만 세면 [X, Y]이고 1번(Y)을 0번(X) 자리로 옮긴 것이다.
            // 잘못된 간선을 자식으로 셌다면 [X, None, Y]가 되어 1번이 None을 가리켰을 것이다.
            Assert.That(ChildrenOf(links, P), Is.EqualTo(new[] { Y, X, NodeId.None }));
        }
    }
}
