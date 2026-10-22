using System;
using UnityEngine;

public class AnimationArmHandler : AnimationHandler
{
    public PosePreset[] LeftMoveRightPoses;
    public PosePreset[] RightMoveLeftPoses;
    public PosePreset[] LeftPoses;
    public PosePreset[] RightPoses;

    public int[] LeftPoseIndexes;
    public int[] RightPoseIndexes;

    private void Start()
    {
        if (InputArmController != null && InputArmController.armController != null && InputArmController.armController.PoseHandler != null)
        {
            InputArmController.armController.PoseHandler.onArmHandle += ResetHandFlags;
        }
    }

    private void OnDestroy()
    {
        if (InputArmController != null && InputArmController.armController != null && InputArmController.armController.PoseHandler != null)
        {
            InputArmController.armController.PoseHandler.onArmHandle -= ResetHandFlags;
        }
    }

    private void ResetHandFlags()
    {
        LeftUp = false;
        RightUp = false;
    }

    public override void HandleLeftHandAnimation(bool cancelled)
    {
        var poseHandler = InputArmController != null && InputArmController.armController != null
            ? InputArmController.armController.PoseHandler
            : null;

        if (poseHandler == null)
            return;

        if (cancelled)
        {
            poseHandler.SetPose(0); // Rest pose
            LeftUp = false;
            return;
        }

        if (!LeftUp && !RightUp)
        {
            poseHandler.Animation(LeftPoseIndexes, timeBetween);
            LeftUp = true;
        }
    }

    public override void HandleRightHandAnimation(bool cancelled)
    {
        var poseHandler = InputArmController != null && InputArmController.armController != null
            ? InputArmController.armController.PoseHandler
            : null;

        if (poseHandler == null)
            return;

        if (cancelled)
        {
            poseHandler.SetPose(0);
            RightUp = false;
            return;
        }

        if (!RightUp && !LeftUp)
        {
            poseHandler.Animation(RightPoseIndexes, timeBetween);
            RightUp = true;
        }
    }

    public override void HandleBothHandsAnimation(bool cancelled)
    {
        if (!cancelled)
        {
            RightUp = true;
            LeftUp = true;
        }
        else
        {
            RightUp = false;
            LeftUp = false;
            if (InputArmController != null && InputArmController.armController != null && InputArmController.armController.PoseHandler != null)
            {
                InputArmController.armController.PoseHandler.CurrentTargetPose = InputArmController.armController.PoseHandler.RestPose;
            }
        }
    }
}