using UC;
using UnityEngine;

public class ScaleOnBeat : BeatSync
{
    [SerializeField] private Vector3    startScale = Vector3.one;
    [SerializeField] private Vector3    endScale = Vector3.one;
    [SerializeField] private float      scaleTime = 1f;

    Tweener.BaseInterpolator currentInterpolator;
    protected override void RunBeat()
    {
        if ((currentInterpolator != null) && (!currentInterpolator.isFinished))
        {
            currentInterpolator.Complete(true, true);
        }

        transform.localScale = startScale;
        currentInterpolator = transform.ScaleTo(endScale, scaleTime).Done(() => currentInterpolator = null);
    }
}
