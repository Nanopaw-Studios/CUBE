using UnityEngine;

public class AnimationPistolHandler : AnimationHandler
{

    public int[] PoseIndexes;

    public override void HandleLeftHandAnimation(bool cancelled)
    {
        if (!cancelled)
        {
            InputArmController.armController.PoseHandler.SpawnedObject.GetComponent<Weapon>().ShootGun();
        }
    }

    public override void HandleRightHandAnimation(bool cancelled)
    {
        if (!cancelled)
        {
            Debug.Log("Aim");
            if (!RightUp && !LeftUp)
            {
                InputArmController.armController.PoseHandler.Animation(PoseIndexes, timeBetween);
                RightUp = true;
            }
        }
        else
        {
            InputArmController.armController.PoseHandler.SetPose(0);
            RightUp = false;
        }
    }

    public override void HandleBothHandsAnimation(bool cancelled)
    {
       
    }
}

