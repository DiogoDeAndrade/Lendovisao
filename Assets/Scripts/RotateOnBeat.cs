using UC;
using UnityEngine;

public class RotateOnBeat : BeatSync
{
    [SerializeField] private float      startRotate = 0.0f;
    [SerializeField] private float      endRotate = 0.0f;
    [SerializeField] private float      rotateTime = 1f;

    Tweener.BaseInterpolator currentInterpolator;
    protected override void RunBeat()
    {
        if ((currentInterpolator != null) && (!currentInterpolator.isFinished))
        {
            currentInterpolator.Complete(true, true);
        }

        transform.localRotation = Quaternion.Euler(0.0f, 0.0f, startRotate);
        currentInterpolator = transform.RotateTo(Quaternion.Euler(0.0f, 0.0f, endRotate), rotateTime).Done(() => currentInterpolator = null);
    }
}
