using System.Collections.Generic;
using UnityEngine;

namespace BehaviorDesigner.Runtime.Tasks
{
    [TaskDescription("switch case to")]
    [TaskIcon("{SkinColor}RandomSelectorIcon.png")]
    public class SwitchCaseSelector : Composite
    {
        [Tooltip("执行点名称")]
        public SharedString executeName;
        
        [Tooltip("没找到可执行点，默认执行点名称")]
        public string defaultName;
        
        private int currentChildIndex = -1;
        private int _defaultIndex = -1;
        // The task status of every child task.
        private TaskStatus executionStatus = TaskStatus.Inactive;

        int GetDefaultIndex()
        {
            if (_defaultIndex < 0)
            {
                for (int i = 0; i < children.Count; ++i) 
                {
                    if (defaultName == children[i].FriendlyName)
                    {
                        _defaultIndex = i;
                        break;
                    }
                }
            }

            return _defaultIndex;
        }
        
        public override void OnStart()
        {
            currentChildIndex = SwitchCaseChildIndex();
        }

        int SwitchCaseChildIndex()
        {
            for (int i = 0; i < children.Count; ++i) 
            {
                if (executeName.Value == children[i].FriendlyName)
                {
                    return i;
                }
            }

            return GetDefaultIndex();
        }

        public override int CurrentChildIndex()
        {
            // Use the execution order list in order to determine the current child index.
            return currentChildIndex;
        }

        public override bool CanExecute()
        {
            // We can continue to execuate as long as we have children that haven't been executed and no child has returned success.
            return currentChildIndex >= 0 && currentChildIndex < children.Count && executionStatus != TaskStatus.Success;
        }

        public override void OnChildExecuted(TaskStatus childStatus)
        {
            // Increase the child index and update the execution status after a child has finished running.
            executionStatus = childStatus;
        }

        public override void OnConditionalAbort(int childIndex)
        {
            // Set the current child index to the index that caused the abort
            currentChildIndex = SwitchCaseChildIndex();
            executionStatus = TaskStatus.Inactive;
        }

        public override void OnEnd()
        {
            currentChildIndex = SwitchCaseChildIndex();
            executionStatus = TaskStatus.Inactive;
        }
    }
}