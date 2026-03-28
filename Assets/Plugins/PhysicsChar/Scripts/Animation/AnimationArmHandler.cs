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

    public override void HandleLeftHandAnimation(bool cancelled)
    {
        Debug.Log("LEFT HANDLER CALLED"); // 👈 ADD THIS
        var poseHandler = InputArmController.armController.PoseHandler;

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
        Debug.Log("RIGHT HANDLER CALLED"); // 👈 ADD THIS
        var poseHandler = InputArmController.armController.PoseHandler;

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
            InputArmController.armController.PoseHandler.CurrentTargetPose = InputArmController.armController.PoseHandler.RestPose;
        }
    }
}