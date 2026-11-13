using System.Collections.Generic;
using UnityEngine;

namespace GuardVsMannequin.Player
{
    /// <summary>없는 파라미터를 설정해 경고가 쌓이지 않도록 컨트롤러에 있는 파라미터 이름만 모은다.</summary>
    public sealed class AnimatorParameterSet
    {
        private readonly HashSet<string> names = new();

        public Animator Animator { get; }

        /// <summary>animator.parameters는 호출마다 배열을 새로 만들어서 처음 한 번만 이름을 모아 둔다.</summary>
        /// <param name="animator">컨트롤러를 지정한 뒤에 넘겨야 파라미터가 들어 있다.</param>
        public AnimatorParameterSet(Animator animator)
        {
            Animator = animator;
            foreach (AnimatorControllerParameter parameter in animator.parameters)
                names.Add(parameter.name);
        }

        /// <summary>
        /// 외형마다 지원하는 동작이 달라서, 호출하는 쪽이 대체 방식을 고를 수 있도록 있는지 묻는다.
        /// </summary>
        public bool Has(string parameterName)
        {
            return names.Contains(parameterName);
        }

        /// <summary>
        /// 없는 파라미터에 설정하면 경고가 매 프레임 쌓이므로 있을 때만 설정한다.
        /// </summary>
        public void SetFloatIfExists(string parameterName, float value)
        {
            if (Has(parameterName))
                Animator.SetFloat(parameterName, value);
        }

        /// <summary>
        /// 호출하는 쪽이 대체 동작을 고를 수 있도록, 트리거가 없으면 경고 없이 false를 준다.
        /// </summary>
        public bool TrySetTrigger(string parameterName)
        {
            if (!Has(parameterName))
                return false;
            Animator.SetTrigger(parameterName);
            return true;
        }

        /// <summary>
        /// 호출하는 쪽이 대체 동작을 고를 수 있도록, 파라미터가 없으면 경고 없이 false를 준다.
        /// </summary>
        public bool TrySetInteger(string parameterName, int value)
        {
            if (!Has(parameterName))
                return false;
            Animator.SetInteger(parameterName, value);
            return true;
        }

        /// <summary>
        /// 호출하는 쪽이 대체 동작을 고를 수 있도록, 파라미터가 없으면 경고 없이 false를 준다.
        /// </summary>
        public bool TrySetBool(string parameterName, bool value)
        {
            if (!Has(parameterName))
                return false;
            Animator.SetBool(parameterName, value);
            return true;
        }
    }
}
