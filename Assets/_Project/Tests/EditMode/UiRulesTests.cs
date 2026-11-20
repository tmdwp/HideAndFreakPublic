using GuardVsMannequin.Core;
using GuardVsMannequin.Settings;
using GuardVsMannequin.UI;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

namespace GuardVsMannequin.Tests
{
    /// <summary>
    /// 나가기 확인·단계 알림·키 맞바꾸기처럼 화면 동작을 정하는 규칙을 확인한다.
    /// </summary>
    public class UiRulesTests
    {
        /// <summary>되돌릴 수 없는 경우(게임 중 게스트, 다른 사람이 있는 방장)에만 묻고 나머지는 바로 나가는지 확인한다.</summary>
        [Test]
        public void LeaveConfirm_AsksOnlyWhenLeavingHurts()
        {
            Assert.AreEqual("confirm.leave.inGame", LeaveConfirmRules.GetWarningKey(isHost: false, isInGame: true, otherMemberCount: 3));
            Assert.AreEqual("confirm.leave.host", LeaveConfirmRules.GetWarningKey(isHost: true, isInGame: true, otherMemberCount: 1));
            Assert.AreEqual("confirm.leave.host", LeaveConfirmRules.GetWarningKey(isHost: true, isInGame: false, otherMemberCount: 2));
            Assert.IsNull(LeaveConfirmRules.GetWarningKey(isHost: false, isInGame: false, otherMemberCount: 3));
            Assert.IsNull(LeaveConfirmRules.GetWarningKey(isHost: true, isInGame: true, otherMemberCount: 0));
        }

        /// <summary>판 시작과 헌트 타임 시작만 알리고, 도중에 들어온 사람에게 지난 헌트 타임을 알리지 않는지 확인한다.</summary>
        [Test]
        public void PhaseAnnouncement_AnnouncesMatchStartAndHuntOnly()
        {
            Assert.IsTrue(PhaseAnnouncementRules.ShouldAnnounce(GameState.Preparation, GameState.Playing));
            Assert.IsTrue(PhaseAnnouncementRules.ShouldAnnounce(GameState.Waiting, GameState.Playing));
            Assert.IsTrue(PhaseAnnouncementRules.ShouldAnnounce(GameState.Playing, GameState.Hunting));
            Assert.IsFalse(PhaseAnnouncementRules.ShouldAnnounce(GameState.Waiting, GameState.Hunting));
            Assert.IsFalse(PhaseAnnouncementRules.ShouldAnnounce(GameState.Hunting, GameState.Ended));
            Assert.IsFalse(PhaseAnnouncementRules.ShouldAnnounce(GameState.Playing, GameState.Playing));
        }

        /// <summary>같은 키를 넣으면 겹친 동작이 이전 키를 받아 두 동작이 모두 쓸 수 있게 남는지 확인한다.</summary>
        [Test]
        public void BindingConflict_SwapsWithActionUsingSameKey()
        {
            InputActionAsset actions = CreateActions(out InputAction primary, out InputAction reload, out InputAction spectate);
            try
            {
                KeyBindingCatalog.TryFind("Primary", null, out KeyBindingCatalog.Entry primaryEntry);
                primary.ApplyBindingOverride(0, "<Keyboard>/r");

                bool swapped = BindingConflictResolver.TrySwap(actions, primaryEntry, "<Mouse>/leftButton", out KeyBindingCatalog.Entry swappedWith);

                Assert.IsTrue(swapped);
                Assert.AreEqual("Reload", swappedWith.actionName);
                Assert.AreEqual("<Mouse>/leftButton", reload.bindings[0].effectivePath);
            }
            finally
            {
                Object.DestroyImmediate(actions);
            }
        }

        /// <summary>관전 키는 살아 있는 동안 쓰지 않아 게임 조작 키와 같아도 맞바꾸지 않는지 확인한다.</summary>
        [Test]
        public void BindingConflict_IgnoresSpectateKeys()
        {
            InputActionAsset actions = CreateActions(out InputAction primary, out _, out InputAction spectate);
            try
            {
                KeyBindingCatalog.TryFind("Primary", null, out KeyBindingCatalog.Entry primaryEntry);
                primary.ApplyBindingOverride(0, "<Keyboard>/rightArrow");

                Assert.IsFalse(BindingConflictResolver.TrySwap(actions, primaryEntry, "<Mouse>/leftButton", out _));
                Assert.AreEqual("<Keyboard>/rightArrow", spectate.bindings[0].effectivePath);
            }
            finally
            {
                Object.DestroyImmediate(actions);
            }
        }

        /// <summary>
        /// 실제 입력 에셋에 기대지 않도록 시험에 필요한 동작 세 개만 가진 에셋을 만든다.
        /// </summary>
        private static InputActionAsset CreateActions(out InputAction primary, out InputAction reload, out InputAction spectate)
        {
            InputActionAsset actions = ScriptableObject.CreateInstance<InputActionAsset>();
            InputActionMap map = actions.AddActionMap("Player");
            primary = map.AddAction("Primary", binding: "<Mouse>/leftButton");
            reload = map.AddAction("Reload", binding: "<Keyboard>/r");
            spectate = map.AddAction("SpectateNext", binding: "<Keyboard>/rightArrow");
            return actions;
        }
    }
}
